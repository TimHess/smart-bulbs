using LinqToTwitter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Pivotal.Discovery.Client;
using Pivotal.Discovery.Eureka;
using SmartBulbs.Common;
using Steeltoe.CloudFoundry.Connector;
using Steeltoe.CloudFoundry.Connector.Services;
using Steeltoe.Common.Discovery;
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
            Environment.SetEnvironmentVariable("VCAP_SERVICES", @"{
      'p-service-registry': [
        {
          'credentials': {
            'uri': 'https://eureka-aefc0bdc-3405-4a90-b977-94738da5a8c4.apps.beet.springapps.io',
            'client_secret': 'tBlAZFxVXTA2',
            'client_id': 'p-service-registry-5e3446d6-912e-4d24-b6b8-e4825c5d8d95',
            'access_token_uri': 'https://p-spring-cloud-services.uaa.cf.beet.springapps.io/oauth/token'
          },
          'syslog_drain_url': null,
          'volume_mounts': [],
          'label': 'p-service-registry',
          'provider': null,
          'plan': 'standard',
          'name': 'myDiscoveryService',
          'tags': [
            'eureka',
            'discovery',
            'registry',
            'spring-cloud'
          ]
        }
      ]
    }");
            // Bootstrap the app
            var builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .AddCloudFoundry();
            Configuration = builder.Build();

            var factory = new LoggerFactory();
            factory.AddConsole(Configuration.GetSection("Logging"));

            var serviceCollection = new ServiceCollection();
            //IServiceInfo info = Configuration.GetServiceInfo<EurekaServiceInfo>("myDiscoveryService");
            //EurekaServiceInfo einfo = info as EurekaServiceInfo;
            //var clientSection = Configuration.GetSection(EurekaClientOptions.EUREKA_CLIENT_CONFIGURATION_PREFIX);
            //serviceCollection.Configure<EurekaClientOptions>(clientSection);
            //serviceCollection.PostConfigure<EurekaClientOptions>((options) =>
            //{
            //    PivotalEurekaConfigurer.UpdateConfiguration(Configuration, einfo, options);
            //});

            var services = serviceCollection
                .AddDiscoveryClient(Configuration)
                .BuildServiceProvider();

            //var clientConfig = new EurekaClientOptions();
            //ConfigurationBinder.Bind(Configuration.GetSection("eureka:client"), clientConfig);
            //EurekaServiceInfo einfo = Configuration.GetServiceInfo<EurekaServiceInfo>("myDiscoveryService");
            //PivotalEurekaConfigurer.UpdateConfiguration(Configuration, einfo, clientConfig);

            //// Create the Eureka client, start fetching registry in background thread
            //var client = new DiscoveryClient(clientConfig, null, factory);
            //var apps = client.Applications;
            //var api = apps.GetRegisteredApplication("SmartBulbs-Web");

            //if (api?.Instances.Any() != true)
            //{
            //    Console.WriteLine($"Eureka Urls attempted: {clientConfig.EurekaServerServiceUrls}");
            //    throw new Exception("Discovery failed");
            //}

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
            //DiscoveryHttpClientHandler _handler = new DiscoveryHttpClientHandler(discoveryClient, factory.CreateLogger<DiscoveryHttpClientHandler>());
            //var httpClient = new HttpClient(_handler, false);
            var httpClient = new HttpClient();

            // begin monitoring
            Console.WriteLine("Entering the loop...");
            while (true)
            {

                Console.WriteLine("Checking for tweets...");
                //string searchTerm = "#cfsummit -coldfusion";
                string searchTerm = "#internationalwomensday2018";

                List<Status> searchResponse =
                    await (from s in ctx.Search where s.Query == searchTerm && s.Type == SearchType.Search && s.IncludeEntities == true && s.TweetMode == TweetMode.Extended && s.SinceID == sinceId && s.Count == 5 select s.Statuses).SingleOrDefaultAsync();

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
                Thread.Sleep(30000);
            }
        }
    }
}
