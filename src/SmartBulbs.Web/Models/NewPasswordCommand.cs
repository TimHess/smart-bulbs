using Microsoft.Extensions.Logging;
using Steeltoe.CircuitBreaker.Hystrix;
using Steeltoe.Security.DataProtection.CredHub;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SmartBulbs.Web.Models
{
    public class NewPasswordCommand : HystrixCommand<string>
    {
        private static readonly Random rand = new Random();
        private ILoggerFactory logFactory;
        private PasswordGenerationParameters _options;
        private static Random random = new Random();

        public NewPasswordCommand(PasswordGenerationParameters options, ILoggerFactory loggerFactory) : base(HystrixCommandGroupKeyDefault.AsKey("NewPasswordGroup"))
        {
            _options = options;
            logFactory = loggerFactory;
        }

        protected override async Task<string> RunAsync()
        {
            var credHubClient = await CredHubClient.CreateMTLSClientAsync(new CredHubOptions { ValidateCertificates = false }, logFactory.CreateLogger("CredHub"));
            var credRequest = new PasswordGenerationRequest("credbulb", _options, overwriteMode: OverwiteMode.overwrite);
            var newPassword = (await credHubClient.GenerateAsync<PasswordCredential>(credRequest)).Value;
            Console.WriteLine("success path");
            return newPassword.ToString();
        }

        protected override Task<string> RunFallbackAsync()
        {
            Console.WriteLine("fallback path");
            string chars = "";
            if (_options.ExcludeUpper != true)
            {
                chars += "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            }
            if (_options.ExcludeLower != true)
            {
                chars += "abcdefghijklmnopqrstuvwxyz";
            }
            if (_options.ExcludeNumber != true)
            {
                chars += "0123456789";
            }
            if (_options.IncludeSpecial == true)
            {
                chars += "!@#$%^&*()~`[]{}\\|;:'\",<.>/?";
            }
            if (chars == "")
            {
                return Task.FromResult("bad request!");
            }
            return Task.FromResult(new string(Enumerable.Repeat(chars, _options.Length ?? 5)
              .Select(s => s[random.Next(s.Length)]).ToArray()));
        }
    }
}
