using Microsoft.Extensions.Hosting;

using System;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Logging;
using System.Linq;
using Quartz;
using Microsoft.Extensions.DependencyInjection;
using Quartz.Impl.Matchers;
using System.Collections.Generic;

namespace AudibleSyncService
{
    public class ScheduleLoggerWorker : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<ScheduleLoggerWorker> _logger;

        public ScheduleLoggerWorker
        (
            IServiceProvider sp,
            ILogger<ScheduleLoggerWorker> logger

        )
        {
            _sp = sp;
            _logger = logger;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var loggedOnce = false;

            var items = Enumerate(4).GroupBy(x => x.key, x=>x.fireTime);

            await foreach (var item in items)
            {
                var runTimes = await item.ToListAsync();
                _logger.LogInformation($"Next fireTimes of {item.Key} will be ({string.Join(", ", runTimes)})");
            }

            if (!loggedOnce)
            {
                _logger.LogInformation($"No next fire time detected");
            }

        }

        private async IAsyncEnumerable<(string key, DateTimeOffset fireTime)> Enumerate(int count = 10)
        {
            var factory = _sp.GetService<ISchedulerFactory>();
            var schedulers = await factory.GetAllSchedulers();
            foreach (var scheduler in schedulers)
            {
                var keys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup());
                foreach (var key in keys)
                {
                    var triggers = await scheduler.GetTriggersOfJob(key);
                    foreach (var trigger in triggers)
                    {
                        DateTimeOffset? nextFireTimeUtc = null;
                        for (int i = 0; i < count; i++)
                        {
                            nextFireTimeUtc = nextFireTimeUtc == null ? trigger.GetNextFireTimeUtc() : trigger.GetFireTimeAfter(nextFireTimeUtc);
                            if (!nextFireTimeUtc.HasValue)
                            {
                                break;
                            }
                            yield return (KeyToString(key), nextFireTimeUtc.Value);

                            //var nextFireTime = nextFireTimeUtc.Value.ToLocalTime();
                            //_logger.LogInformation($"FireTime of '{KeyToString(key)}' will be {nextFireTime} ({nextFireTimeUtc} UTC)");
                            //loggedOnce = true;

                        }
                    }
                }
            }

            //return loggedOnce;
        }

        private static string KeyToString(JobKey key)
        {
            if (string.IsNullOrEmpty(key.Group))
            {
                return $"[{key.Group}] {key.Name}";
            }
            return $"{key.Name}";
        }
    }
}
