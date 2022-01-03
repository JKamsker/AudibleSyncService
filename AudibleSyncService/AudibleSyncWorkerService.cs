
using AudibleApi;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using System;
using System.IO;
using System.Threading.Tasks;

using WorkerService.Models.Configuration;
using System.Threading;
using System.Net.Http;
using AAXClean;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;
using System.Collections.Generic;
using Open.ChannelExtensions;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace AudibleSyncService
{
    public class AudibleSyncWorkerService : BackgroundService
    {
        private readonly AudibleConfig _config;
        private readonly AudibleApiFactory _factory;
        private readonly ILogger<AudibleSyncWorkerService> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private Api _apiClient;

        public AudibleSyncWorkerService
        (
            IOptions<AudibleConfig> options,
            AudibleApiFactory factory,
            ILogger<AudibleSyncWorkerService> logger,
            ILoggerFactory loggerFactory
        )
        {
            _config = options.Value;
            _factory = factory;
            _logger = logger;
            _loggerFactory = loggerFactory;
        }
        private async ValueTask<Api> GetClientAsync()
        {
            return _apiClient ??= await _factory.GetApiAsync();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            try
            {
                _logger.LogInformation($"Headless: {_config.Headless}");
                _logger.LogInformation($"Logging in with user '{_config.Credentials.UserName}'");
                await SyncNewAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, $"Sync failed: {ex.Message}");
                throw;
            }

            _logger.LogInformation("Sync done");
        }

        private async Task SyncNewAsync(CancellationToken token)
        {
            var client = await GetClientAsync();
            await EchoUserInfo(client);

            var items = client
                .EnumerateLibraryItemsAsync();

            var books = items
              .Select(item => new AudibleBook(client, item, _config.Environment, _loggerFactory.CreateLogger<AudibleBook>()))
              .Where(x => !x.IsSeries)
              .Where(x => !x.OutputExists())
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
                .ReadAll(cancellationToken: token, x => x.Dispose(), deferredExecution: true);

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

        private async Task EchoUserInfo(Api client)
        {
            try
            {
                var profile = await client.UserProfileAsync();
                var email = profile["email"].Value<string>();

                _logger.LogInformation($"Logged in with user: '{email}'");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Error while trying to echo user info:  {ex}");
            }

        }

        private async Task SyncAsync()
        {
            var client = await GetClientAsync();
            var items = client
                .EnumerateLibraryItemsAsync();


            await foreach (var item in items)
            {
                using var book = new AudibleBook(client, item, _config.Environment, _loggerFactory.CreateLogger<AudibleBook>());
                if (item is { IsEpisodes: true, LengthInMinutes: 0, AvailableCodecs: null })
                {
                    _logger.LogInformation($"{book.Identifier}: Skipping - No audio");
                    continue;
                }

                try
                {
                    await DownloadAndConvert(client, item);
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, $"{book.Identifier}: Download failed: {ex.Message}");
                }
            }

            async Task DownloadAndConvert(Api client, AudibleApi.Common.Item item)
            {
                using var book = new AudibleBook(client, item, _config.Environment, _loggerFactory.CreateLogger<AudibleBook>());

                if (book.OutputExists())
                {
                    _logger.LogInformation($"{book.Identifier} skipping: Output already exists");
                    return;
                }

                _logger.LogInformation($"{book.Identifier} Downloading");
                await book.DownloadAsync();

                _logger.LogInformation($"{book.Identifier} Decrypting singlePart file");
                await book.DecryptAsync();

                _logger.LogInformation($"{book.Identifier} Moving file to output");
                book.MoveToOutput();
            }
        }
    }
}
