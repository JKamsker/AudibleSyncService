global using Rnd.IO.Extensions;
global using Rnd.Lib.Extensions;
global using Rnd.Logging;

using AudibleApi;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.IO;

using WorkerService.Models.Configuration;

using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Linq;

using System.Text;
using System.Collections.Generic;
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
            Host
                .CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, builder) =>
                {
                    builder.SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                        .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                        .AddEnvironmentVariables();
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
                .Build().Run();
        }



        private static void ConfigureServices(HostBuilderContext ctx, IServiceCollection services)
        {
            services
                .Configure<AudibleConfig>(ctx.Configuration.GetSection("Audible"))
                .RegisterApi()
                .AddHostedService<AudibleSyncWorkerService>();
        }
    }

    public static class DependencyInjectionExtensions
    {
        public static IServiceCollection RegisterApi(this IServiceCollection services)
        {
            return services
                .AddTransient<ILoginCallback, LoginCallback>()
                .AddTransient<AudibleApiFactory>()
                ;
        }
    }
}
