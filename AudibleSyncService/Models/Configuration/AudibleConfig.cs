using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorkerService.Models.Configuration
{
    public class AudibleConfig
    {
        public bool Headless { get; set; } = true;
        public bool Setup { get; set; } = false;

        public bool RunOnce { get; set; } = true;

        public string Locale { get; set; }
        //public string ScheduleExpression { get; set; }
        public AudibleCredentials Credentials { get; set; }
        public AudibleEnvironment Environment { get; set; }
        public ScheduleConfig Schedule { get; set; }
    }

    public class ScheduleConfig
    {
        public string Expression { get; set; }
        public bool? RunImmediately { get; set; }
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
        public bool UseFFmpeg { get; set; }

        public static string EvaluateSettingsBasePath(AudibleEnvironment audibleEnvironment)
        {
            return string.IsNullOrEmpty(audibleEnvironment?.SettingsBasePath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "audibleSyncWorker")
                : audibleEnvironment.SettingsBasePath;
        }
    }

}
