using LinqToTwitter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Pivotal.Discovery.Client;
using Pivotal.Extensions.Configuration.ConfigServer;
using SmartBulbs.Common;
using Steeltoe.Common.Discovery;
using Steeltoe.Common.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TwitterMonitor
{
    public class Program
    {
        private static IConfiguration Configuration;

        public static async Task Main(string[] args)
        {
            // Bootstrap the app
            var builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .AddConfigServer();
            Configuration = builder.Build();

            var factory = new LoggerFactory();
            factory.AddConsole(Configuration.GetSection("Logging"));

            var services = new ServiceCollection()
                .AddDiscoveryClient(Configuration)
                .AddOptions()
                .BuildServiceProvider();

            TwitterCredentials twitterCreds = new TwitterCredentials();
            Configuration.GetSection("Twitter").Bind(twitterCreds);

            var auth = new SingleUserAuthorizer
            {
                CredentialStore = new InMemoryCredentialStore
                {
                    ConsumerKey = twitterCreds.ConsumerKey,
                    ConsumerSecret = twitterCreds.ConsumerSecret,
                    OAuthToken = twitterCreds.AccessToken,
                    OAuthTokenSecret = twitterCreds.AccessTokenSecret
                }
            };
            await auth.AuthorizeAsync();
            var ctx = new TwitterContext(auth);
            ulong sinceId = 1;

            // next line Fails, not able to resolve IOptionsMonitor<EurekaClientOptions>
            var discoveryClient = services.GetRequiredService<IDiscoveryClient>();
            DiscoveryHttpClientHandler _handler = new DiscoveryHttpClientHandler(discoveryClient, factory.CreateLogger<DiscoveryHttpClientHandler>());
            var httpClient = new HttpClient(_handler, false);

            // begin monitoring
            Console.WriteLine("Entering the loop...");
            while (true)
            {
                string searchTerm = Configuration.GetValue<string>("twitterSearch");
                Console.WriteLine($"Checking for 10 tweets with query '{searchTerm}'");

                List<Status> searchResponse =
                    await (from s in ctx.Search where s.Query == searchTerm && s.Type == SearchType.Search && s.IncludeEntities == true && s.TweetMode == TweetMode.Extended && s.SinceID == sinceId && s.Count == 10 select s.Statuses).SingleOrDefaultAsync();

                if (searchResponse.Any())
                {
                    sinceId = searchResponse.Max(s => s.StatusID);
                    var texts = searchResponse.Select(t => t.FullText);
                    Console.WriteLine($"Found {texts.Count()} tweets");
                    foreach (var t in texts)
                    {
                        Console.WriteLine(t);
                    }

                    // post to web api
                    var apiResponse = await httpClient.PostAsync("http://SmartBulbs-Web/home/bulktext", new StringContent(JsonConvert.SerializeObject(texts), Encoding.UTF8, "application/json"));
                    if (apiResponse.IsSuccessStatusCode)
                    {
                        var responseBody = await apiResponse.Content.ReadAsJsonAsync<ColorChangeResponse>();
                        Console.WriteLine($"Aggregate sentiment value was {responseBody.Sentiment} which translates to #{responseBody.HexColor}");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Request failed, status code: {apiResponse.StatusCode}");
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                }
                else
                {
                    Console.WriteLine("No new tweets found");
                }
                Thread.Sleep(Configuration.GetValue<int>("sleepTime"));
            }
        }
    }
}
