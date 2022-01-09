
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

            //var myBooks = items.Where(x=>x.Series(m))

            var books = items
              //.Where(x => x.Series?.Any(m => m.Title.Contains("Bob")) == true)
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
