using CredBulb.Models;
using LinqToTwitter;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Steeltoe.Common.Http;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CredBulb.Controllers
{
    public class HomeController : Controller
    {
        private NewColorCommand _colorCommand;
        private static HttpClient _httpClient;
        private JsonSerializerSettings _jsonSettings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
        private string _iftttUrl = "https://maker.ifttt.com/trigger/custom_light_up/with/key/{0}";
        private string _sentimentUrl = "https://eastus2.api.cognitive.microsoft.com/text/analytics/v2.0/sentiment";
        private string _cognitiveKey;
        private string _ConsumerKey;
        private string _ConsumerSecret;
        private string _AccessToken;
        private string _AccessTokenSecrete;

        public HomeController(IConfiguration config, NewColorCommand newColorCommand)
        {
            _colorCommand = newColorCommand;
            _httpClient = new HttpClient();
            _iftttUrl = string.Format(_iftttUrl, config.GetValue(typeof(string), "iftttKey"));
            _cognitiveKey = config.GetValue<string>("cognitiveServicesKey");
            _ConsumerKey = config.GetValue<string>("Twitter:ConsumerKey");
            _ConsumerSecret = config.GetValue<string>("Twitter:ConsumerSecret");
            _AccessToken = config.GetValue<string>("Twitter:AccessToken");
            _AccessTokenSecrete = config.GetValue<string>("Twitter:AccessTokenSecret");
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CredHubColorize()
        {
            // call credhub to generate a hex password
            var color = await _colorCommand.ExecuteAsync();

            // call ifttt to colorize
            var response = await _httpClient.PostAsJsonAsync($"{_iftttUrl}", 
                new IftttWebhookPayload {
                    Value1 = color,
                    Value2 = "1" }, 
                _jsonSettings);

            // return results
            Thread.Sleep(1500);
            return Json(color);
        }

        [HttpPost]
        public async Task<IActionResult> LightByText([FromBody]string text)
        {
            var response = new ColorChangeResponse { TextInput = text };

            // post text to cognitive services api
            var message = new HttpRequestMessage {
                RequestUri = new Uri(_sentimentUrl),
                Method = HttpMethod.Post,
                Headers =
                {
                    { "Ocp-Apim-Subscription-Key", _cognitiveKey },
                    { "Accept", "application/json" }
                },
                Content = new StringContent($"{{\"documents\":[{{\"language\":\"en\",\"id\":\"1\",\"text\":\"{text}\"}}]}}", Encoding.UTF8, "application/json")
            };
            var sentimentHttpResponse = await _httpClient.SendAsync(message);
            SentimentResponse sentimentResponseData = await sentimentHttpResponse.Content.ReadAsJsonAsync<SentimentResponse>();
            sentimentResponseData.Documents.First().TryGetValue("score", out string scoreResponse);
            response.Sentiment = double.Parse(scoreResponse);

            // turn result into color
            var red = response.Sentiment - 1;
            var green = response.Sentiment;
            double blue;
            if (response.Sentiment < .5)
            {
                blue = response.Sentiment;
            }
            else if (response.Sentiment == .5)
            {
                blue = 1;
            }
            else
            {
                blue = 1 - response.Sentiment;
            }
            response.HexColor = Hexicolor(red) + Hexicolor(green) + Hexicolor(blue);

            // post to ifttt
            var ifTTTresponse = await _httpClient.PostAsJsonAsync($"{_iftttUrl}",
                new IftttWebhookPayload
                {
                    Value1 = response.HexColor,
                    Value2 = "1"
                },
                _jsonSettings);

            // return sentiment + color value
            Thread.Sleep(1500);
            return Json(response);
        }

        [HttpPost]
        public async Task<IActionResult> TwitterHandle()
        {
            try
            {
                var auth = new SingleUserAuthorizer
                {
                    CredentialStore = new InMemoryCredentialStore
                    {
                        ConsumerKey = _ConsumerKey,
                        ConsumerSecret = _ConsumerSecret,
                        OAuthToken = _AccessToken,
                        OAuthTokenSecret = _AccessTokenSecrete
                    }
                };
                await auth.AuthorizeAsync();

                var ctx = new TwitterContext(auth);
                if (auth == null)
                {

                }
                string searchTerm = "#cfsummit -coldfusion";

                Search searchResponse =
                    await
                    (from search in ctx.Search
                     where search.Type == SearchType.Search &&
                           search.Query == searchTerm &&
                           search.IncludeEntities == true &&
                           search.TweetMode == TweetMode.Extended
                     select search)
                    .SingleOrDefaultAsync();
                //var tweets =
                //    await
                //    (from tweet in ctx.Status
                //     where tweet.Type == StatusType.Home &&
                //           tweet.TweetMode == TweetMode.Extended && tweet.FullText.Contains("#cfsummit")
                //     select new TweetViewModel
                //     {
                //         ImageUrl = tweet.User.ProfileImageUrl,
                //         ScreenName = tweet.User.ScreenNameResponse,
                //         Text = tweet.FullText
                //     })
                //    .ToListAsync();
                return Json(searchResponse);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private string Hexicolor(double value)
        {
            if (value < 0)
            {
                value *= -1;
            }

            value *= 255;

            if (value > 255)
            {
                value = 255; 
            }

            return Convert.ToInt16(value).ToString("X2");
        }
    }
}
