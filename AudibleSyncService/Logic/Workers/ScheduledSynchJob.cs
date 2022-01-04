
using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Quartz;

namespace AudibleSyncService
{
    public class ScheduledSynchJob : IJob
    {
        private readonly AudibleSyncService _syncService;
        private readonly ApiLockService _apiLockService;
        private readonly ILogger<ScheduledSynchJob> _logger;

        public ScheduledSynchJob
        (
            AudibleSyncService syncService,
            ApiLockService apiLockService,
            ILogger<ScheduledSynchJob> logger
        )
        {
            _syncService = syncService;
            _apiLockService = apiLockService;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("Executing sync because of a trigger");

            var nextFireTimeUtc = context.Trigger.GetNextFireTimeUtc();
            var nextFireTime = nextFireTimeUtc.Value.ToLocalTime();
            _logger.LogInformation($"NextFireTime will be {nextFireTime} ({nextFireTimeUtc} UTC)");

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
                    await _syncService.ExecuteAsync(context.CancellationToken);
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
