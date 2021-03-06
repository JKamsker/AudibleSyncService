using Microsoft.Extensions.Hosting;

using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Http;
using AAXClean;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Dinah.Core.Collections.Generic;

namespace AudibleSyncService
{
    public class AudibleSyncWorkerService : BackgroundService
    {
        private readonly AudibleSyncService _syncService;
        private readonly ApiLockService _apiLockService;
        private readonly ILogger<AudibleSyncWorkerService> _logger;

        public AudibleSyncWorkerService
        (
            AudibleSyncService syncService,
            ApiLockService apiLockService,
            ILogger<AudibleSyncWorkerService> logger
        )
        {
            _syncService = syncService;
            _apiLockService = apiLockService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Executing sync because of startup");

            try
            {
                var email = await _syncService.GetEmailAsync();
                if (!_apiLockService.TryLock(email))
                {
                    _logger.LogInformation("A sync process is already in progress. Doing nothing ...");
                    return;
                }
                try
                {
                    await _syncService.ExecuteAsync(stoppingToken);
                }
                finally
                {
                    _apiLockService.TryUnlock(email);
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, $"Sync failed: {ex.Message}");
                throw;
            }

        }
    }
}
