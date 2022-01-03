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

namespace AudibleSyncService
{

    // ffmpeg.exe -audible_key [key] -audible_iv [iv] -i audiobook.aaxc -map_metadata 0 -id3v2_version 3 -codec:a copy -vn "audiobook.m4b"
    internal class Program
    {
        static async Task Main(string[] args)
        {
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
            services
                .Configure<AudibleConfig>(ctx.Configuration.GetSection("Audible"))
                .RegisterApi();

            if (ctx.Configuration.GetSection("AUDIBLE:SETUP").Get<bool>())
            {
                services.AddHostedService<AudibleSetupService>();
            }
            else
            {
                services.AddHostedService<AudibleSyncWorkerService>();
            }
        }
    }
}
