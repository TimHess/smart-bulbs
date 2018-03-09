using LinqToTwitter;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SmartBulbs.Web.Models;
using SmartBulbs.Common;
using Steeltoe.Common.Http;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SmartBulbs.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace SmartBulbs.Web.Controllers
{
    public class HomeController : Controller
    {
        private NewColorCommand _colorCommand;
        private static HttpClient _httpClient;
        private JsonSerializerSettings _jsonSettings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
        private string _iftttUrl = "https://maker.ifttt.com/trigger/custom_light_up/with/key/{0}";
        private string _sentimentUrl = "https://eastus2.api.cognitive.microsoft.com/text/analytics/v2.0/sentiment";
        private string _cognitiveKey;
        private TwitterCredentials _twitterCreds;
        private IHubContext<ObservationHub> _hubContext;

        public HomeController(IConfiguration config, NewColorCommand newColorCommand, IOptionsSnapshot<TwitterCredentials> twitterCreds, IHubContext<ObservationHub> hubContext)
        {
            _colorCommand = newColorCommand;
            _httpClient = new HttpClient();
            _iftttUrl = string.Format(_iftttUrl, config.GetValue(typeof(string), "iftttKey"));
            _cognitiveKey = config.GetValue<string>("cognitiveServicesKey");
            _twitterCreds = twitterCreds.Value;
            _hubContext = hubContext;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Observe()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CredHubColorize()
        {
            // call credhub to generate a hex password
            var color = await _colorCommand.ExecuteAsync();
            if (_colorCommand.IsResponseFromFallback)
            {
                Console.WriteLine("Response is not from CredHub");
            }

            // call ifttt to colorize
            var response = await _httpClient.PostAsJsonAsync($"{_iftttUrl}", 
                new IftttWebhookPayload {
                    Value1 = color,
                    Value2 = "1" }, 
                _jsonSettings);

            await _hubContext.Clients.All.SendAsync("Messages", new List<ColorChangeResponse> { new ColorChangeResponse { HexColor = color, TextInput = "CredHub Password" } });

            // return results
            Thread.Sleep(1500);
            return Json(color);
        }

        [HttpPost]
        public async Task<IActionResult> LightByText([FromBody]string text)
        {
            var response = new ColorChangeResponse { TextInput = text };

            var analysis = await ColorBySentiment(new List<string> { text });

            response.Sentiment = analysis.First().Item2;
            response.HexColor = analysis.First().Item3;

            // post to ifttt
            var ifTTTresponse = await _httpClient.PostAsJsonAsync($"{_iftttUrl}",
                new IftttWebhookPayload
                {
                    Value1 = response.HexColor,
                    Value2 = "1"
                },
                _jsonSettings);

            await _hubContext.Clients.All.SendAsync("Messages", new List<ColorChangeResponse>{ response });

            // return sentiment + color value
            Thread.Sleep(1500);
            return Json(response);
        }

        [HttpPost]
        public async Task<IActionResult> BulkText([FromBody]List<string> texts)
        {
            var analysis = await BulkSentiment(texts);

            await _hubContext.Clients.All.SendAsync("Messages", analysis);

            var response = new ColorChangeResponse { TextInput = "Bulk Analysis" };
            response.Sentiment = analysis.Average(i => i.Sentiment);
            response.HexColor = HexColorFromDecimal(response.Sentiment);

            // post to ifttt
            var ifTTTresponse = await _httpClient.PostAsJsonAsync($"{_iftttUrl}",
                new IftttWebhookPayload
                {
                    Value1 = response.HexColor,
                    Value2 = "1"
                },
                _jsonSettings);

            // return sentiment + color value
            return Json(response);
        }

        [HttpPost]
        public async Task<IActionResult> CheckTwitter()
        {
            try
            {
                var auth = new SingleUserAuthorizer
                {
                    CredentialStore = new InMemoryCredentialStore
                    {
                        ConsumerKey = _twitterCreds.ConsumerKey,
                        ConsumerSecret = _twitterCreds.ConsumerSecret,
                        OAuthToken = _twitterCreds.AccessToken,
                        OAuthTokenSecret = _twitterCreds.AccessTokenSecret
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

                var texts = searchResponse.Statuses.Select(t => t.FullText);
                var analyzed = await ColorBySentiment(texts);

                var toReturn = new List<EnhancedTwitterStatus>();
                var toBroadcast = new List<ColorChangeResponse>();
                foreach (var s in searchResponse.Statuses)
                {
                    var analysis = analyzed.Find(a => a.Item1 == s.FullText);
                    toReturn.Add(new EnhancedTwitterStatus {
                        FullText = s.FullText,
                        CreatedAt = s.CreatedAt,
                        User = s.User,
                        SentimentValue = analysis.Item2,
                        HexColor = analysis.Item3 });
                    toBroadcast.Add(new ColorChangeResponse { TextInput = s.FullText, Sentiment = analysis.Item2, HexColor = analysis.Item3 });
                }
                await _hubContext.Clients.All.SendAsync("Messages", toBroadcast);

                return Json(toReturn);
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

        /// <summary>
        /// Analyze text and provide sentiment analysis and color values for each string
        /// </summary>
        /// <param name="texts">Strings to analyze</param>
        /// <returns>Sentiment analysis and color values with original strings</returns>
        private async Task<List<Tuple<string, double, string>>> ColorBySentiment(IEnumerable<string> texts)
        {
            var toReturn = new List<Tuple<string, double, string>>();

            var analysis = await BulkSentiment(texts);
            foreach (var r in analysis)
            {
                toReturn.Add(new Tuple<string, double, string>(r.TextInput, r.Sentiment, HexColorFromDecimal(r.Sentiment)));
            }

            return toReturn;
        }

        /// <summary>
        /// Call Azure Cognitive Services' Sentiment Analysis Api
        /// </summary>
        /// <param name="texts">List of strings to score</param>
        /// <returns>List of strings with their sentiment scores</returns>
        private async Task<List<ScoredText>> BulkSentiment(IEnumerable<string> texts)
        {
            var toReturn = new List<ScoredText>();

            var i = 1;
            var docList = string.Empty;

            foreach (var t in texts)
            {
                // scrub user content so the api call won't fail
                var cleanText = t.Replace("\r", "").Replace("\n", "").Replace('"', '\'');

                // build the request body - Cognitive services requires a unique integer per item
                docList += $"{{\"language\":\"en\",\"id\":\"{i}\",\"text\":\"{cleanText}\"}},";
                i++;
            }
            docList = docList.Substring(0, docList.Length - 1);
            var message = new HttpRequestMessage
            {
                RequestUri = new Uri(_sentimentUrl),
                Method = HttpMethod.Post,
                Headers =
                {
                    { "Ocp-Apim-Subscription-Key", _cognitiveKey },
                    { "Accept", "application/json" }
                },
                Content = new StringContent($"{{\"documents\":[{docList}]}}", Encoding.UTF8, "application/json")
            };
            var sentimentHttpResponse = await _httpClient.SendAsync(message);
            SentimentResponse sentimentResponseData = await sentimentHttpResponse.Content.ReadAsJsonAsync<SentimentResponse>();
            i = 0;
            foreach (var r in sentimentResponseData.Documents)
            {
                var sentimentParsible = r.TryGetValue("score", out string scoreResponse);
                var sentiment = double.Parse(scoreResponse);
                toReturn.Add(new ScoredText { TextInput = texts.ElementAt(i), Sentiment = sentiment });
                i++;
            }

            return toReturn;
        }


        /// <summary>
        /// Convert a decimal (between 0 and 1) to an RGB value
        /// </summary>
        /// <param name="sentiment">Sentiment value</param>
        /// <returns>RGB color value</returns>
        /// <remarks>The scale is red(0) to green(1), with blue values highest at .5 and lower towards 0 or 1</remarks>
        private string HexColorFromDecimal(double sentiment)
        {
            var red = sentiment - 1;
            var green = sentiment;
            double blue;
            if (sentiment < .5)
            {
                blue = sentiment;
            }
            else if (sentiment == .5)
            {
                blue = 1;
            }
            else
            {
                blue = 1 - sentiment;
            }
            return Hexicolor(red) + Hexicolor(green) + Hexicolor(blue);
        }

        /// <summary>
        /// Converts a decimal to a hex value
        /// </summary>
        /// <param name="value">Value to convert</param>
        /// <returns>Hex value</returns>
        /// <remarks>Values are altered to min 0 and max of 255 if outside those bounds</remarks>
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
