
using AudibleApi;
using Microsoft.Extensions.Options;

using System;
using System.IO;
using System.Threading.Tasks;

using WorkerService.Models.Configuration;
using Microsoft.Extensions.Logging;

namespace AudibleSyncService
{
    public class AudibleApiFactory
    {
        private readonly ILoginCallback _loginCallback;
        private readonly ILogger<AudibleApiFactory> _logger;
        private readonly AudibleConfig _config;

        public AudibleApiFactory
        (
            ILoginCallback loginCallback,
            IOptions<AudibleConfig> config,
            ILogger<AudibleApiFactory> logger
        )
        {
            _loginCallback = loginCallback;
            _logger = logger;
            _config = config.Value;
        }

        public bool IdentityExists()
        {
            var settingsBasePath = GetConfigPath();
            var identityFile = Path.Combine(settingsBasePath, "identity.json");
            return File.Exists(identityFile);
        }

        public async Task<Api> GetApiAsync()
        {
            var settingsBasePath = GetConfigPath();

            Directory.CreateDirectory(settingsBasePath);
            _logger.LogInformation($"Using settings basePath: '{settingsBasePath}'");

            var identityFile = Path.Combine(settingsBasePath, "identity.json");
            var jsonPath = Path.Combine(settingsBasePath, "apiSettings.json");
            if (!File.Exists(jsonPath))
            {
                jsonPath = null;
            }

            return await EzApiCreator.GetApiAsync
            (
                _loginCallback,
                Localization.Get(_config.Locale),
                identityFile,
                jsonPath
            );
        }

        private string GetConfigPath() => AudibleEnvironment.EvaluateSettingsBasePath(_config.Environment);
    }
}
