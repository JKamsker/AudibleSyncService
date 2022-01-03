
using AudibleApi;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.ComponentModel;
using System.IO;

using WorkerService.Models.Configuration;

namespace AudibleSyncService
{
    public class LoginCallback : ILoginCallback
    {
        private readonly AudibleConfig _options;
        private readonly ILogger<LoginCallback> _logger;

        public LoginCallback
        (
            IOptions<AudibleConfig> options,
            ILogger<LoginCallback> logger
        )
        {
            _options = options.Value;
            _logger = logger;
        }
        public string Get2faCode()
        {
            ThrowIfHeadless("2FA");
            Console.Write("Please enter your 2FA code:");
            var _2faCode = Console.ReadLine();
            return _2faCode;
        }

        public string GetCaptchaAnswer(byte[] captchaImage)
        {
            ThrowIfHeadless("Chaptcha");

            //throw new NotImplementedException("Chaptcha is not available in headless mode");
            var tempPath = string.IsNullOrEmpty(_options.Environment.TempPath)
                ? Path.Combine(Path.GetTempPath(), "audibleSyncWorker")
                : _options.Environment.TempPath;

            var captchaPath = Path.Combine(tempPath, "audible_api_captcha_" + Guid.NewGuid() + ".jpg");
            try
            {
                File.WriteAllBytes(captchaPath, captchaImage);

                Console.WriteLine($"Saved captcha image to '{captchaPath}'");
                Console.Write("CAPTCHA answer: ");
                var guess = Console.ReadLine();

                return guess;
            }
            finally
            {
                if (File.Exists(captchaPath))
                    File.Delete(captchaPath);
            }
        }

        public (string email, string password) GetLogin()
        {
            var (email, password) = (_options.Credentials.UserName, _options.Credentials.Password);
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ThrowIfHeadless("Credentials");

                Console.Write("Email: ");
                email = Console.ReadLine();
                Console.Write("Password: ");
                password = Console.ReadLine();
            }

            return (email, password);
        }

        //
        // not all parts are implemented for demo app
        //
        public (string name, string value) GetMfaChoice(MfaConfig mfaConfig)
        {
            ThrowIfHeadless("Multi-Factor-Auth");

            return default;
        }

        public void ShowApprovalNeeded()
        {
            ThrowIfHeadless("Approval");
        }

        private void ThrowIfHeadless(string name)
        {
            if (_options.Headless)
            {
                throw new NotImplementedException($"{name} is not available in headless mode. Please turn Headless mode off and follow the authorization flow.");
            }
        }
    }
}
