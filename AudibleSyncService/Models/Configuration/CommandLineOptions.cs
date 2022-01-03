using CommandLine;

namespace AudibleSyncService
{
    public class CommandLineOptions
    {
        [Option('s', "setup", Required = false, HelpText = "Sets up identityfile")]
        public bool Setup { get; set; }
    }
}
