
using AudibleApi;

using Microsoft.Extensions.Configuration;

using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using CommandLine;


using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.IO;

using WorkerService.Models.Configuration;

using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Linq;

using System.Text;
using Microsoft.Extensions.Logging;
using System.Drawing;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using System;
using Quartz;
using System.Collections.Specialized;

namespace AudibleSyncService
{
    public static class DependencyInjectionExtensions
    {
        public static IServiceCollection RegisterApi(this IServiceCollection services)
        {
            return services
                .AddTransient<ILoginCallback, LoginCallback>()
                .AddSingleton<AudibleApiFactory>()
                ;
        }


        public static void EnableSetupFromArgs(this IConfigurationBuilder builder, ParserResult<CommandLineOptions> parsed)
        {
            parsed.WithParsed<CommandLineOptions>(x =>
            {
                if (!x.Setup)
                {
                    return;
                }

                builder.AddInMemoryCollection(new List<KeyValuePair<string, string>>()
                {
                    new KeyValuePair<string, string>("AUDIBLE:SETUP" , "true"),
                    new KeyValuePair<string, string>("AUDIBLE:HEADLESS" , "false"),
                });
            });
        }


        public static void AddScheduledExecution(this IServiceCollection services, IConfiguration configuration)
        {
            var audibleConfiguration = configuration.GetSection("Audible").Get<AudibleConfig>();
            var cronExpression = audibleConfiguration?.Schedule?.Expression;
            if (string.IsNullOrEmpty(cronExpression))
            {
                Console.WriteLine("No schedule expression configured");
                return;
            }


            // base configuration from appsettings.json
            services.Configure<QuartzOptions>(configuration.GetSection("Quartz"));

            // if you are using persistent job store, you might want to alter some options
            services.Configure<QuartzOptions>(options =>
            {
                options.Scheduling.IgnoreDuplicates = true; // default: false
                options.Scheduling.OverWriteExistingData = true; // default: true
            });

            services.AddQuartz(q =>
            {
                // handy when part of cluster or you want to otherwise identify multiple schedulers
                q.SchedulerId = "Scheduler-Core";

                // we take this from appsettings.json, just show it's possible
                // q.SchedulerName = "Quartz ASP.NET Core Sample Scheduler";

                // as of 3.3.2 this also injects scoped services (like EF DbContext) without problems
                q.UseMicrosoftDependencyInjectionJobFactory();

                // or for scoped service support like EF Core DbContext
                // q.UseMicrosoftDependencyInjectionScopedJobFactory();

                // quickest way to create a job with single trigger is to use ScheduleJob
                // (requires version 3.2)
                q.ScheduleJob<ScheduledSynchJob>(trigger => trigger
                    .WithIdentity("AudibleSyncJob")
                    //.ForJob(new JobKey("SyncJob"))
                    //.WithCronSchedule("0/3 * * * * ?")
                    .StartAt(DateBuilder.EvenSecondDate(DateTimeOffset.UtcNow.AddSeconds(3)))

                    .WithCronSchedule(cronExpression)
                    //.StartNow()

                    //.WithCronSchedule("1 0 * * * ?")
                    //.StartAt(DateTime.Today.AddDays(1))

                    .WithDescription("my awesome trigger configured for a job with single call")
                    
                    //.StartNow()
                    
                );

                var settingsBasePath = AudibleEnvironment.EvaluateSettingsBasePath(audibleConfiguration.Environment);

                // these are the defaults
                q.UseSimpleTypeLoader();
                q.UseInMemoryStore();
                q.UseDefaultThreadPool(tp => tp.MaxConcurrency = 1);

            });

            // we can use options pattern to support hooking your own configuration
            // because we don't use service registration api, 
            // we need to manually ensure the job is present in DI
            services.AddTransient<ScheduledSynchJob>();

            // Quartz.Extensions.Hosting allows you to fire background service that handles scheduler lifecycle
            services.AddQuartzHostedService(options =>
            {
                // when shutting down we want jobs to complete gracefully
                options.WaitForJobsToComplete = true;
            });

            services.AddHostedService<ScheduleLoggerWorker>();



        }


    }
}
