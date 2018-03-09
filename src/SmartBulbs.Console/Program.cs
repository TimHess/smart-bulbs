using LinqToTwitter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SmartBulbs.Common;
using Steeltoe.Common.Http;
using Steeltoe.Discovery.Eureka;
using Steeltoe.Extensions.Configuration.CloudFoundry;
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
                .AddCloudFoundry();
            Configuration = builder.Build();

            var factory = new LoggerFactory();
            factory.AddConsole(Configuration.GetSection("Logging"));

            TwitterCredentials twitterCreds = new TwitterCredentials();
            Configuration.GetSection("Twitter").Bind(twitterCreds);

            // Build Eureka clients config from configuration
            var discoveryConfig = new EurekaClientConfig();
            Configuration.GetSection("eureka:client").Bind(discoveryConfig);

            // Create the Eureka client, start fetching registry in background thread
            var discovery = new DiscoveryClient(discoveryConfig, null, factory);
            var apps = discovery.Applications;
            var api = apps.GetRegisteredApplication("SmartBulbs-Web");

            if (api.Count == 0)
            {
                throw new Exception("Discovery failed");
            }

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

            var httpClient = new HttpClient();
            ulong sinceId = 1;

            // begin monitoring
            Console.WriteLine("Entering the loop...");
            while (true)
            {
                Console.WriteLine("Checking for tweets...");
                //string searchTerm = "#cfsummit -coldfusion";
                string searchTerm = "#internationalwomensday2018";

                List<Status> searchResponse =
                    await
                    (from search in ctx.Search
                     where search.Type == SearchType.Search &&
                           search.Query == searchTerm &&
                           search.IncludeEntities == true &&
                           search.TweetMode == TweetMode.Extended && 
                           search.SinceID == sinceId
                     select search.Statuses)
                    .SingleOrDefaultAsync();

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
                    if (api != null)
                    {
                        var apiResponse = await httpClient.PostAsync(api.Instances[0].HomePageUrl + "home/bulktext", new StringContent(JsonConvert.SerializeObject(texts), Encoding.UTF8, "application/json"));
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
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Unable to find api server!");
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                }
                else
                {
                    Console.WriteLine("No new tweets found");
                }
                Thread.Sleep(60000);
            }
        }
    }
}
