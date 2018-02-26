using CredBulb.Models;
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

        public HomeController(IConfiguration config, NewColorCommand newColorCommand)
        {
            _colorCommand = newColorCommand;
            _httpClient = new HttpClient();
            _iftttUrl = string.Format(_iftttUrl, config.GetValue(typeof(string), "iftttKey"));
            _cognitiveKey = config.GetValue<string>("cognitiveServicesKey");
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
            var rgb = Color.FromArgb(Convert.ToInt32(response.Sentiment * 100000));
            if (response.Sentiment < .3)
            {
                response.HexColor = "FF0000";
            }
            else if(response.Sentiment >= .3 && response.Sentiment < .7)
            {
                response.HexColor = "0000FF";
            }
            else
            {
                response.HexColor = "00FF00";
            }

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

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
