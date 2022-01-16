
using AudibleApi;
using Microsoft.Extensions.Options;

using System;
using System.Threading.Tasks;

using WorkerService.Models.Configuration;
using System.Threading;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;
using Open.ChannelExtensions;
using System.Linq;
using FFMpegCore;

using MediaChannel = FFMpegCore.Enums.Channel;
using ATL;
using Microsoft.Extensions.DependencyInjection;
using AudibleSyncService.Logic.Services;

namespace AudibleSyncService
{
    public class AudibleSyncService
    {
        private readonly AudibleConfig _config;
        private readonly AudibleApiFactory _factory;
        private readonly ILogger<AudibleSyncService> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IServiceProvider _sp;
        private Api _apiClient;
        private string _cachedEmail = string.Empty;

        public AudibleSyncService
        (
            IOptions<AudibleConfig> options,
            AudibleApiFactory factory,
            ILogger<AudibleSyncService> logger,
            ILoggerFactory loggerFactory,
            IServiceProvider sp
        )
        {
            _config = options.Value;
            _factory = factory;
            _logger = logger;
            _loggerFactory = loggerFactory;
            _sp = sp;
        }

        public async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"Headless: {_config.Headless}");
            _logger.LogInformation($"Setup: {_config.Setup}");

            _logger.LogInformation($"Logging in with user '{_config.Credentials.UserName}'");
            await SyncNewAsync(stoppingToken);
            _logger.LogInformation("Sync done");

            if (_config.RunOnce)
            {
                _logger.LogInformation("Exiting because of the Audible.RunOnce");
                await Task.Delay(250);
                Environment.Exit(0);
            }
        }

        private async ValueTask<Api> GetClientAsync()
        {
            if (_apiClient is not null)
            {
                return _apiClient;
            }

            _logger.LogInformation("Validating identity file");
            if (_config.Headless && !_factory.IdentityExists())
            {
                throw new Exception("Identity file not found or invalid. Please turn Headless mode off and follow the authorization flow.");
            }

            return (_apiClient = await _factory.GetApiAsync());
        }

        private async Task SyncNewAsync(CancellationToken token)
        {
            var client = await GetClientAsync();
            await EchoUserInfo();

            var items = client
                .EnumerateLibraryItemsAsync();

            //var response = await client.GetLibraryBookAsync("B08W5C9RJL", LibraryOptions.ResponseGroupOptions.ALL_OPTIONS);

            //var newName = @"F:\tmp\AudibleSync\analysis\Der Abgrund jenseits der Träume\output - Kopie.m4b";
            //var track = new Track(newName);
            //_sp.GetService<Tagger>().TryTagFile(newName.AsFileInfo(), response);

            var books = items
              //.Where(x => x.Series?.Any(m => m.Title.Contains("Physiker")) == true)
              //.Where(x=>x.Asin == "B07GS66BZX")
              .Where(x => x.ContentDeliveryType != "content_delivery_type") // Series??
              //.Select(item => new AudibleBook(client, item, _config.Environment, _loggerFactory.CreateLogger<AudibleBook>()))
              .Select(item => ActivatorUtilities.CreateInstance<AudibleBook>(_sp, client, item))
              .Where(x => !x.IsSeries)
              .Where(x => !x.OutputExists())
              //.Take(1)

              ;

            await Channel
                .CreateBounded<AudibleBook>(2)
                .Source(books, cancellationToken: token)
                .PipeAsync
                (
                    maxConcurrency: 1,
                    capacity: 1,
                    transform: async x =>
                    {
                        await Download(x, token);
                        return x;
                    },
                    cancellationToken: default
                )
                .Filter(x => !x.Disposed && x.DownloadSuccessful)
                .PipeAsync
                (
                    maxConcurrency: 1,
                    capacity: 1,
                    transform: async book =>
                    {
                        return await Decrypt(book, token);
                    },
                    cancellationToken: default
                )
                .ReadAll(cancellationToken: token, x =>
                {
                    _logger.LogInformation($"{x.Identifier}: Finished Sync");
                    x.Dispose();
                }, deferredExecution: true);

            async Task Download(AudibleBook book, CancellationToken token)
            {
                try
                {
                    _logger.LogInformation($"{book.Identifier} Downloading");
                    await book.DownloadAsync(token);
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, $"{book.Identifier}: Download failed: {ex.Message}");
                    book.Dispose();
                }
            }

            async ValueTask<AudibleBook> Decrypt(AudibleBook book, CancellationToken token)
            {
                try
                {
                    _logger.LogInformation($"{book.Identifier} Decrypting singlePart file");
                    await book.DecryptAsync(token);

                    _logger.LogInformation($"{book.Identifier} Moving file to output");
                    book.MoveToOutput();
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, $"{book.Identifier}: Decryption failed: {ex.Message}");
                    book.Dispose();
                }

                return book;
            }
        }

        private async Task Test(Api client)
        {
            //var item = await client.GetLibraryBookAsync("3837146618", LibraryOptions.ResponseGroupOptions.ALL_OPTIONS);

            //var book = new AudibleBook(client, item, _config.Environment, _loggerFactory.CreateLogger<AudibleBook>())
            //    .OverwriteTempId(Guid.Empty);

            //if (!book.EncryptedFile.Exists)
            //{
            //    await book.DownloadAsync();
            //}

            //var track = new Track(book.DecryptedFile.FullName);

            //var lic = await book.GetLicense();
            // ffmpeg.exe -audible_key [key] -audible_iv [iv] -i audiobook.aaxc -map_metadata 0 -id3v2_version 3 -codec:a copy -vn "audiobook.m4b"

            //await FFMpegArguments.FromFileInput(book.EncryptedFile, x => x.WithAudibleEncryptionKeys(lic.Key, lic.Iv))
            //    .MapMetaData()
            //    .OutputToFile(book.DecryptedFile.FullName, true, x => x.WithTagVersion(3).DisableChannel(MediaChannel.Video).CopyChannel(MediaChannel.Audio))
            //    .ProcessAsynchronously();

            //await book.DecryptAsync();

         
            //await FFMpegArguments
            // .FromFileInput(track.SourcePath)
            // .OutputToFile(currentFile.FullName, true, x => x.WithAudioCodec("libfdk_aac").WithVariableBitrate(4).DisableChannel(Channel.Video))
            // .ProcessAsynchronously();

            Environment.Exit(0);
        }

        public async ValueTask<string> GetEmailAsync()
        {
            if (!string.IsNullOrEmpty(_cachedEmail))
            {
                return _cachedEmail;
            }

            var client = await GetClientAsync();
            return _cachedEmail = await client.GetEmailAsync();
        }

        private async Task EchoUserInfo()
        {
            try
            {
                var email = await GetEmailAsync();
                _logger.LogInformation($"Logged in with user: '{email}'");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Error while trying to echo user info:  {ex}");
            }
        }

        private async Task SyncAsync()
        {
            throw new NotImplementedException();
            var client = await GetClientAsync();
            var items = client
                .EnumerateLibraryItemsAsync();


            //await foreach (var item in items)
            //{
            //    using var book = new AudibleBook(client, item, _config.Environment, _loggerFactory.CreateLogger<AudibleBook>());
            //    if (item is { IsEpisodes: true, LengthInMinutes: 0, AvailableCodecs: null })
            //    {
            //        _logger.LogInformation($"{book.Identifier}: Skipping - No audio");
            //        continue;
            //    }

            //    try
            //    {
            //        await DownloadAndConvert(client, item);
            //    }
            //    catch (Exception ex)
            //    {
            //        _logger.LogCritical(ex, $"{book.Identifier}: Download failed: {ex.Message}");
            //    }
            //}

            //async Task DownloadAndConvert(Api client, AudibleApi.Common.Item item)
            //{
            //    using var book = new AudibleBook(client, item, _config.Environment, _loggerFactory.CreateLogger<AudibleBook>());

            //    if (book.OutputExists())
            //    {
            //        _logger.LogInformation($"{book.Identifier} skipping: Output already exists");
            //        return;
            //    }

            //    _logger.LogInformation($"{book.Identifier} Downloading");
            //    await book.DownloadAsync();

            //    _logger.LogInformation($"{book.Identifier} Decrypting singlePart file");
            //    await book.DecryptAsync();

            //    _logger.LogInformation($"{book.Identifier} Moving file to output");
            //    book.MoveToOutput();
            //}
        }
    }
}
