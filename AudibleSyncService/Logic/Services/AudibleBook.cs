using AAXClean;

using ATL;

using AudibleApi;
using AudibleApi.Common;

using Microsoft.Extensions.Logging;

using Rnd.Lib.Utils;

using System;
using System.Collections.Generic;
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
            ILogger<AudibleBook> logger
        )
        {
            _apiClient = api;
            _item = item;
            _audibleEnvironment = audibleEnvironment;
            _logger = logger;
        }

        public Guid TempId { get; } = Guid.NewGuid();

        public FileInfo EncryptedFile => GetTempDirectory().GetFile("audiobook.aaxc");
        public FileInfo DecryptedFile => GetTempDirectory().GetFile("audiobook.m4b");

        private DirectoryInfo GetTempDirectory()
        {
            var path = string.IsNullOrEmpty(_audibleEnvironment.TempPath)
                ? Path.Combine(Path.GetTempPath(), "audibleSyncWorker")
                : _audibleEnvironment.TempPath;

            return path.AsDirectoryInfo().CreateSubdirectory(TempId.ToString()).EnsureCreated();
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

                _license = (contentLic?.Voucher?.Key, contentLic?.Voucher?.Iv);
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

        public async Task DecryptAsync(CancellationToken token = default)
        {
            if (_state != DownloadState.Downloaded)
            {
                throw new InvalidOperationException("File has not been downloaded.");
            }

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

            //using var tagFile = TagLib.File.Create(DecryptedFile.FullName);
            //tagFile.Tag.AmazonId = _item.Asin;
            //tagFile.Tag.Composers = _item.Narrators?.Select(x => x.Name).ToArray();
            //tagFile.Tag.AlbumArtists = _item.Authors?.Select(x => x.Name).ToArray();

            //tagFile.Save();

            try
            {
                var track = new Track(DecryptedFile.FullName);
                track.AdditionalFields = new Dictionary<string, string>();

                track.Artist = _item.Authors?.Select(x => x.Name).JoinString("; ") ?? String.Empty;
                track.Title = _item.Title;
                track.Album = _item.Title;

                if (_item.Subtitle is not null)
                {
                    track.AdditionalFields["TIT3"] = _item.Subtitle;
                    track.AdditionalFields["----:com.apple.iTunes:subtitle"] = _item.Subtitle;
                }

                if (_item.PublisherName is not null)
                {
                    track.Publisher = _item.PublisherName;
                    track.AdditionalFields["----:com.apple.iTunes:publisher"] = _item.PublisherName;
                }

                track.Year = _item.DatePublished?.Year;
                track.Composer = _item.Narrators?.Select(x => x.Name).JoinString("; ") ?? String.Empty;

                if (_item.Description is not null)
                {
                    var description = _item.Description;
                    description = Regex.Replace(description, "<.*?>", String.Empty);

                    track.Description = description;
                    track.AdditionalFields["----:com.apple.iTunes:description"] = track.Description;
                }

                track.Genre = _item.Categories?.Select(x => x.Name).JoinString("; ") ?? string.Empty;

                var series = _item.Series?.FirstOrDefault();
                if (series != null)
                {
                    track.AdditionalFields["----:com.apple.iTunes:series"] = series.SeriesName;
                    track.AdditionalFields["----:com.apple.iTunes:series-part"] = series.Sequence;
                }

                track.AdditionalFields["CDEK"] = _item.Asin;
                track.AdditionalFields["----:com.apple.iTunes:ASIN"] = _item.Asin;

                track.AdditionalFields["rldt"] = _item.DatePublished?.ToString("dd-MMM-yyyy", new CultureInfo("en-US")) ?? string.Empty;
                track.Save();
            }
            catch (Exception)
            {
                _logger.LogWarning("Tagging failed");
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
