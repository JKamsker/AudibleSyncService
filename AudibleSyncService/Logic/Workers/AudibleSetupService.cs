using Microsoft.Extensions.Hosting;

using System;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WorkerService.Models.Configuration;

namespace AudibleSyncService
{
    public class AudibleSetupService : BackgroundService
    {
        private readonly AudibleConfig _config;
        private readonly AudibleApiFactory _factory;
        private readonly ILogger<AudibleSetupService> _logger;

        public AudibleSetupService
        (
            AudibleApiFactory factory,
            IOptions<AudibleConfig> options,
            ILogger<AudibleSetupService> logger
        )
        {
            _config = options.Value;
            _factory = factory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation($"Headless: {_config.Headless}");
                _logger.LogInformation($"Setup: {_config.Setup}");

                _logger.LogInformation("Welcome to the setup");
                var client = await _factory.GetApiAsync();
                var email = await client.GetEmailAsync();
                _logger.LogInformation($"Logged in with '{email}'! You can continue by restarting the app without the '-setup' flag");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error setting up identity: {ex}");
                await Task.Delay(500);
                Environment.Exit(-1);
            }
            Environment.Exit(0);
        }
    }
}
