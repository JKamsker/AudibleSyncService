
using AudibleApi;

using Microsoft.Extensions.Configuration;

using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using CommandLine;

namespace AudibleSyncService
{
    public static class DependencyInjectionExtensions
    {
        public static IServiceCollection RegisterApi(this IServiceCollection services)
        {
            return services
                .AddTransient<ILoginCallback, LoginCallback>()
                .AddTransient<AudibleApiFactory>()
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
    }
}
