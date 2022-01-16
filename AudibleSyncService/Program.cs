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
using FFMpegCore;
using System.Threading;
using System.Runtime.InteropServices;
using ATL;
using ATL.Logging;
using System.Collections.Generic;
using AudibleSyncService.Logic.Services;
using Microsoft.Extensions.Options;

namespace AudibleSyncService
{
    //$ 
    //$ 
    //$ 
    // ffmpeg.exe -audible_key [key] -audible_iv [iv] -i audiobook.aaxc -map_metadata 0 -id3v2_version 3 -codec:a copy -vn "audiobook.m4b"
    internal class Program
    {

        static async Task<byte[]> GetPicData()
        {
            var url = "https://m.media-amazon.com/images/I/511Sze5gENL._SL500_.jpg";
            var picture = url;// _item.ProductImages.The500;
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(picture);
                var result = await response.Content.ReadAsByteArrayAsync();
                return result;
            }
        }

        static async Task Main(string[] args)
        {
            ATL.Settings.FileBufferSize = 5_000_000;
            //ATL.Settings.MP4_createNeroChapters = false;
            //var log = new LoggingTest();
            var newName = @"F:\tmp\AudibleSync\analysis\Der Abgrund jenseits der Träume\output - Kopie.m4b";

            //var file = @"F:\tmp\AudibleSync\tmp\308b2ea9-9543-49dd-a578-af628a37b1b1\audiobook.aaxc".AsFileInfo();
            //await FFMpegArguments.FromFileInput(file, x => x.WithAudibleEncryptionKeys("b763815c46dedfd64e180d06ac635ae0", "cbb27c67358c273d867607b28e341587"))
            //      .MapMetaData()
            //      .OutputToFile(file.AsFileInfo().Directory.GetFile("").FullName, true, x => x.WithTagVersion(3).CopyChannel(FFMpegCore.Enums.Channel.Both))
            //      .ProcessAsynchronously();

            //var newName = @"F:\tmp\AudibleSync\tmp\308b2ea9-9543-49dd-a578-af628a37b1b1\audiobook.m4b";


            //var track = new Track(newName);
            ////var data = await GetPicData();
            ////track.EmbeddedPictures.Add(PictureInfo.fromBinaryData(data));

            //track.Save();
            //log.TestSyncMessage();


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
                .AddTransient<AudibleEnvironment>(x => x.GetRequiredService<IOptions<AudibleConfig>>().Value.Environment)
                .RegisterApi()
                .AddTransient<AudibleSyncService>()
                .AddSingleton<ApiLockService>()
                .AddSingleton<Tagger>()
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

        private static void TestFiles()
        {
            var files = Directory.GetFiles(@"CENSORED\audiobookshelf\data\audible", "*.m4b", SearchOption.AllDirectories);

            var errors = 0;

            Parallel.ForEach(files, file =>
            {
                try
                {
                    var analysis = FFProbe.Analyse(file);
                    var hasError = analysis.ErrorData.Any(x => x.Contains("Reserved bit set."));
                    if (hasError)
                    {
                        Console.WriteLine($"'{file}' has error");
                        Interlocked.Increment(ref errors);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"'{file}' threw exception! {ex}");


                }

            });

            Console.WriteLine($"{errors} of {files.Length} files are invalid");
        }
    }

    public class LoggingTest : ILogDevice
    {
        Log theLog = new Log();
        List<Log.LogItem> messages = new();

        public LoggingTest()
        {
            LogDelegator.SetLog(ref theLog);
            theLog.Register(this);
        }

        public void TestSyncMessage()
        {
            //messages.Clear();

            LogDelegator.GetLocateDelegate()("file name");
            LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "test message 1");
            LogDelegator.GetLogDelegate()(Log.LV_WARNING, "test message 2");
            foreach (var item in messages)
            {
                Console.WriteLine(item.Message);
            }
            //System.Console.WriteLine(messages[0].Message);
        }

        public void DoLog(Log.LogItem anItem)
        {
            messages.Add(anItem);
        }
    }
}
