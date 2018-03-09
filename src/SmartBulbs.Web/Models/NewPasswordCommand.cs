using Microsoft.Extensions.Logging;
using Steeltoe.CircuitBreaker.Hystrix;
using Steeltoe.Security.DataProtection.CredHub;
using System;
using System.Threading.Tasks;

namespace SmartBulbs.Web.Models
{
    public class NewPasswordCommand : HystrixCommand<string>
    {
        private static readonly Random rand = new Random();
        private ILoggerFactory logFactory;

        public NewPasswordCommand(IHystrixCommandOptions options, ILoggerFactory loggerFactory) : base(options)
        {
            logFactory = loggerFactory;
        }

        protected override async Task<string> RunAsync()
        {
            var credHubClient = await CredHubClient.CreateMTLSClientAsync(new CredHubOptions(), logFactory.CreateLogger("CredHub"));
            var pwparams = new PasswordGenerationParameters { };
            var credRequest = new PasswordGenerationRequest("credbulb", pwparams, overwriteMode: OverwiteMode.overwrite);
            var newPassword = (await credHubClient.GenerateAsync<PasswordCredential>(credRequest)).Value;
            Console.WriteLine("success path");
            return newPassword.ToString();
        }

        protected override Task<string> RunFallbackAsync()
        {
            Console.WriteLine("fallback path");
            return Task.FromResult(Guid.NewGuid().ToString());
        }
    }
}
