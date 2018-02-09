using Steeltoe.CircuitBreaker.Hystrix;
using Steeltoe.Security.DataProtection.CredHub;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CredBulb.Models
{
    public class NewColorCommand : HystrixCommand<string>
    {
        private static readonly Random rand = new Random();

        public NewColorCommand(IHystrixCommandOptions options) : base(options)
        {
        }

        protected override async Task<string> RunAsync()
        {
            var credHubClient = await CredHubClient.CreateMTLSClientAsync(new CredHubOptions());
            var pwparams = new PasswordGenerationParameters { };
            var newPassword = (await credHubClient.GenerateAsync<PasswordCredential>(new PasswordGenerationRequest("color", pwparams))).Value;
            int hash = newPassword.GetHashCode();
            string r = ((hash & 0xFF0000) >> 16).ToString("X2");
            string g = ((hash & 0x00FF00) >> 8).ToString("X2");
            string b = (hash & 0x0000FF).ToString("X2");
            Console.WriteLine("success path");
            return r + g + b;
        }

        protected override Task<string> RunFallbackAsync()
        {
            Console.WriteLine("fallback path");
            return Task.FromResult(rand.Next(256).ToString("X2") + rand.Next(256).ToString("X2") + rand.Next(256).ToString("X2"));
        }
    }
}
