using AAXClean;

using ATL;

using AudibleApi;
using AudibleApi.Common;

using AudibleSyncService.Logic.Services;

using FFMpegCore;

using Microsoft.Extensions.Logging;

using Rnd.Lib.Utils;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Schema;

using WorkerService.Models.Configuration;

namespace AudibleSyncService
{
    public class AudibleBook : IDisposable
    {
        private readonly Api _apiClient;
        private readonly Item _item;
        private readonly AudibleEnvironment _audibleEnvironment;
        private readonly Tagger _tagger;
        private readonly ILogger<AudibleBook> _logger;

        private (string Key, string Iv) _license;

        private DownloadState _state = DownloadState.Default;
        public DownloadState State => _state;

        public string Identifier => $"[{_item.Asin}] {_item.Title}";

        public bool IsSeries => _item is { IsEpisodes: true, LengthInMinutes: 0, AvailableCodecs: null };

        public AudibleBook
        (
            Api api,
            Item item,
            AudibleEnvironment audibleEnvironment,
            Tagger tagger,
            ILogger<AudibleBook> logger
        )
        {
            _apiClient = api;
            _item = item;
            _audibleEnvironment = audibleEnvironment;
            _tagger = tagger;
            _logger = logger;
        }

        public Guid TempId { get; private set; } = Guid.NewGuid();

        public FileInfo EncryptedFile => GetTempDirectory().GetFile("audiobook.aaxc");
        public FileInfo DecryptedFile => GetTempDirectory().GetFile("audiobook.m4b");

        private DirectoryInfo GetTempDirectory(bool create = true)
        {
            var path = string.IsNullOrEmpty(_audibleEnvironment.TempPath)
                ? Path.Combine(Path.GetTempPath(), "audibleSyncWorker")
                : _audibleEnvironment.TempPath;

            var dir = path.AsDirectoryInfo().CreateSubdirectory(TempId.ToString());
            return create ? dir.EnsureCreated() : dir;
        }

        public AudibleBook OverwriteTempId(Guid guid)
        {
            GetTempDirectory(false).EnsureDeleted();
            TempId = guid;
            return this;
        }

        public async ValueTask<(string Key, string Iv)> GetLicense()
        {
            if (_license == default)
            {
                UpdateLicense(await _apiClient.GetDownloadLicenseAsync(_item.Asin));
            }

            return _license;
        }

        public bool DownloadSuccessful => _state == DownloadState.Downloaded;

        public bool Disposed { get; private set; }

        public async Task DownloadAsync(CancellationToken token = default)
        {
            if (_state == DownloadState.Downloading)
            {
                throw new InvalidOperationException("A download is already in progress");
            }
            _state = DownloadState.Downloading;

            try
            {
                var contentLic = await _apiClient.GetDownloadLicenseAsync(_item.Asin);
                //_logger.LogTrace($"OfflineUrl: '{contentLic?.ContentMetadata?.ContentUrl?.OfflineUrl}'");
                var isAdrm = contentLic.DrmType == DrmType.Adrm;

                if (!isAdrm)
                {
                    throw new Exception("Content is not ADRM encrypted");
                }


                using var cli = new HttpClient();
                using var req = new HttpRequestMessage(HttpMethod.Get, contentLic?.ContentMetadata?.ContentUrl?.OfflineUrl)
                {
                    Headers =
                    {
                        { "User-Agent", Resources.USER_AGENT },
                    }
                };

                using var downloadResponse = await cli.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, token);
                downloadResponse.EnsureSuccessStatusCode();

                if (token.IsCancellationRequested)
                {
                    _state = DownloadState.Default;
                    return;
                }

                //_logger.LogInformation("Performing download");
                //var encryptedFile = GetTempDirectory().GetFile("audiobook.aaxc");
                using (var fs = EncryptedFile.EnsureDeleted().OpenWrite())
                {
                    await downloadResponse.Content.CopyToAsync(fs, token);
                }
                //_item.Relationships

                UpdateLicense(contentLic);
                //contentLic.ContentMetadata.ChapterInfo

                _state = DownloadState.Downloaded;
            }
            catch (Exception ex)
            {
                _state = DownloadState.Default;
                throw;
            }
            finally
            {
                if (token.IsCancellationRequested)
                {
                    _state = DownloadState.Default;

                    try
                    {
                        EncryptedFile.EnsureDeleted();
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void UpdateLicense(ContentLicense contentLic)
        {
            _license = (contentLic?.Voucher?.Key, contentLic?.Voucher?.Iv);
        }


        private bool FFMpegExists()
        {
            try
            {
                var path = GlobalFFOptions.GetFFMpegBinaryPath();
                if (string.IsNullOrEmpty(path))
                {
                    return false;
                }

                var codecs = FFMpeg.GetVideoCodecs();
                if (!codecs.Any())
                {
                    return false;
                }
            }
            catch (Exception)
            {

                return false;
            }

            return true;
        }

        public async Task DecryptAsync(CancellationToken token = default)
        {
            if (_state != DownloadState.Downloaded)
            {
                throw new InvalidOperationException("File has not been downloaded.");
            }

            //todo make code not look like shit
            if (_audibleEnvironment.UseFFmpeg && FFMpegExists())
            {
                //await FFMpegArguments.FromFileInput(EncryptedFile, x => x.WithAudibleEncryptionKeys(_license.Key, _license.Iv))
                //    .MapMetaData()
                //    .OutputToFile(DecryptedFile.FullName, true, x => x.WithTagVersion(3).DisableChannel(FFMpegCore.Enums.Channel.Video).CopyChannel(FFMpegCore.Enums.Channel.Audio))
                //    .ProcessAsynchronously();
                _logger.LogInformation("Using FFMPEG to decrypt the audiobook");
                await FFMpegArguments.FromFileInput(EncryptedFile, x => x.WithAudibleEncryptionKeys(_license.Key, _license.Iv))
                    .MapMetaData()
                    .OutputToFile(DecryptedFile.FullName, true, x => x.WithTagVersion(3).CopyChannel(FFMpegCore.Enums.Channel.Both))
                    .ProcessAsynchronously();
            }
            else
            {
                using var aaxcFile = new AaxFile(EncryptedFile.OpenRead());
                aaxcFile.SetDecryptionKey(_license.Key, _license.Iv);

                var chap = aaxcFile.GetChapterInfo();

                var finished = false;
                token.Register(() =>
                {
                    if (finished)
                    {
                        return;
                    }
                    aaxcFile.Cancel();
                });

                var res = aaxcFile.ConvertToMp4a(DecryptedFile.EnsureDeleted().OpenWrite());

                finished = true;
                token.ThrowIfCancellationRequested();

                if (res == ConversionResult.Failed)
                {
                    throw new Exception("Conversion failed");
                }
            }

            _tagger.TryTagFile(DecryptedFile, _item);
        }



        /// <summary>
        /// Todo if needed
        /// </summary>
        /// <returns></returns>
        static async Task<byte[]> GetPicData()
        {
            var url = "https://m.media-amazon.com/images/I/511Sze5gENL._SL500_.jpg";
            var picture = url;// _item.ProductImages.The500;
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(picture);
                var result = await response.Content.ReadAsByteArrayAsync();
                return result;
            }
        }

        public void MoveToOutput()
        {
            //e.g: "%author%\\%series%\\%title%.%ext%"

            var target = GetOutputPath().EnsureParentCreated();

            DecryptedFile.MoveTo(target);

        }

        private FileInfo GetOutputPath()
        {
            var name = new[]
            {
                (key: "%author%", value: _item.Authors?.FirstOrDefault()?.Name),
                (key: "%series%", value: _item.Series?.FirstOrDefault()?.SeriesName),
                (key: "%title%", value: _item.Title),
                (key: "%ext%", value: "m4b"),
            }
            .Select(x => (x.key, value: x.value ?? String.Empty))
            .Select(x => (x.key, value: PathValidation.CleanFileName(x.value)))
            .Aggregate(_audibleEnvironment.OutputPattern, (cur, rule) => cur.Replace(rule.key, rule.value, StringComparison.OrdinalIgnoreCase))
            ;

            return Path
                .Combine(_audibleEnvironment.OutputPath, name)
                .AsFileInfo();
        }

        public bool OutputExists() => GetOutputPath().Exists;

        public void Dispose()
        {
            EncryptedFile.EnsureDeleted();
            DecryptedFile.EnsureDeleted();
            GetTempDirectory().EnsureDeleted(true);
            Disposed = true;

            GC.SuppressFinalize(this);
        }

        ~AudibleBook()
        {
            Dispose();
        }

        public enum DownloadState
        {
            Default,
            Downloading,
            Downloaded
        }
    }
}
