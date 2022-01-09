global using Rnd.IO.Extensions;
global using Rnd.Lib.Extensions;
global using Rnd.Logging;

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

    // ffmpeg.exe -audible_key [key] -audible_iv [iv] -i audiobook.aaxc -map_metadata 0 -id3v2_version 3 -codec:a copy -vn "audiobook.m4b"
    internal class Program
    {
        static async Task Main(string[] args)
        {
            ATL.Settings.FileBufferSize = 5_000_000;
            ATL.Settings.MP4_createNeroChapters = false;

            var parsed = CommandLine.Parser.Default.ParseArguments<CommandLineOptions>(args);

            Host
                .CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, builder) =>
                {
                    builder.SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                        .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                        .AddEnvironmentVariables()
                        .EnableSetupFromArgs(parsed);

                })
                .ConfigureServices(ConfigureServices)
                .ConfigureLogging
                (
                    x => x.ClearProviders()
                        .AddColorConsoleLogger(LogLevel.Warning, Color.DarkMagenta)
                        .AddColorConsoleLogger(LogLevel.Error, Color.Red)
                        .AddColorConsoleLogger(LogLevel.Trace, Color.Gray)
                        .AddColorConsoleLogger(LogLevel.Debug, Color.Gray)
                        .AddColorConsoleLogger(LogLevel.Information, Color.Yellow)
                        .AddColorConsoleLogger(LogLevel.Critical, Color.Red)
                )
                .Build()
                .Run();
        }

        private static void ConfigureServices(HostBuilderContext ctx, IServiceCollection services)
        {
            var audibleConfiguration = ctx.Configuration.GetSection("Audible").Get<AudibleConfig>();

            services
                .Configure<AudibleConfig>(ctx.Configuration.GetSection("Audible"))
                .RegisterApi()
                .AddTransient<AudibleSyncService>()
                .AddSingleton<ApiLockService>()
                ;


            if (audibleConfiguration.Setup)
            {
                services.AddHostedService<AudibleSetupService>();
                return;
            }

            // only false will prevent immediately run
            if (audibleConfiguration?.Schedule?.RunImmediately is true or null)
            {
                services.AddHostedService<AudibleSyncWorkerService>();
            }


            services.AddScheduledExecution(ctx.Configuration);
        }



        private static void QuarzTest01()
        {
            // you can have base properties
            var properties = new NameValueCollection();

            // and override values via builder
            var sched = SchedulerBuilder.Create(properties)
            // default max concurrency is 10
            .UseDefaultThreadPool(x => x.MaxConcurrency = 5)
            // this is the default 
            // .WithMisfireThreshold(TimeSpan.FromSeconds(60))
            .UsePersistentStore(x =>
            {
                // force job data map values to be considered as strings
                // prevents nasty surprises if object is accidentally serialized and then 
                // serialization format breaks, defaults to false
                x.UseProperties = true;
                x.UseClustering();
                // there are other SQL providers supported too 
                x.UseSQLite("my connection string");

                // this requires Quartz.Serialization.Json NuGet package
                x.UseJsonSerializer();
            })
            // job initialization plugin handles our xml reading, without it defaults are used
            // requires Quartz.Plugins NuGet package
            .UseXmlSchedulingConfiguration(x =>
            {
                x.Files = new[] { "~/quartz_jobs.xml" };
                // this is the default
                x.FailOnFileNotFound = true;
                // this is not the default
                x.FailOnSchedulingError = true;
            })
            .BuildScheduler();
        }
    }


}
