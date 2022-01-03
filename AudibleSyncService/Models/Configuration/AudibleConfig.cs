using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorkerService.Models.Configuration
{
    public class AudibleConfig
    {
        public string Locale { get; set; }
        public AudibleCredentials Credentials { get; set; }
        public AudibleEnvironment Environment { get; set; }
    }

    public class AudibleCredentials
    {
        public string UserName { get; set; }
        public string Password { get; set; }
    }

    public class AudibleEnvironment
    {
        public string SettingsBasePath { get; set; }
        public string TempPath { get; set; }

        public string OutputPath { get; set; }
        public string OutputPattern { get; set; }
    }

}
