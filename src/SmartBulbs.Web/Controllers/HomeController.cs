using LifxIoT.Api;
using LifxIoT.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SmartBulbs.Common;
using SmartBulbs.Web.Hubs;
using SmartBulbs.Web.Models;
using SmartBulbs.Web.Services;
using Steeltoe.Security.DataProtection.CredHub;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace SmartBulbs.Web.Controllers
{
    public class HomeController : Controller
    {
        private static HttpClient _httpClient;
        private JsonSerializerSettings _jsonSettings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
        private IHubContext<ObservationHub> _hubContext;
        private LifxApi _lifxClient;
        private Utils _utils;
        private ILoggerFactory _logFactory;

        public HomeController(IConfiguration config, IHubContext<ObservationHub> hubContext, ILoggerFactory loggerFactory)
        {
            _httpClient = new HttpClient();
            _hubContext = hubContext;
            _lifxClient = new LifxApi(config.GetValue<string>("lifxKey"));
            _logFactory = loggerFactory;
            _utils = new Utils(config.GetValue<string>("cognitiveServicesKey"), _httpClient);
        }

        public IActionResult Index()
        {
            var results = new List<string>();
            for (double s = 0; s < 1; s += .02d)
            {
                results.Add(_utils.HexColorFromDouble(s));
            }
            return View(results);
        }

        public IActionResult Sentiment()
        {
            var results = new List<string>();
            for (double s = 0; s < 1; s += .02d)
            {
                results.Add(_utils.HexColorFromDouble(s));
            }
            return View(results);
        }

        public IActionResult Feedback()
        {
            var results = new List<string>();
            for (double s = 0; s < 1; s += .02d)
            {
                results.Add(_utils.HexColorFromDouble(s));
            }
            return View(results);
        }

        public IActionResult Observe()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CredHubColorize([FromBody]PasswordGenerationParameters options)
        {
            if (options == null) { options = new PasswordGenerationParameters(); }

            // call credhub to generate a password
            string newPassword;
            try
            {
                var credHubClient = await CredHubClient.CreateMTLSClientAsync(new CredHubOptions(), _logFactory.CreateLogger("CredHub"));
                var credRequest = new PasswordGenerationRequest("credbulb", options, overwriteMode: OverwiteMode.overwrite);
                newPassword = (await credHubClient.GenerateAsync<PasswordCredential>(credRequest)).Value.ToString();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Calling CredHub Failed: {e}");
                newPassword = Guid.NewGuid().ToString();
                if (options.Length != null && newPassword.Length > options.Length)
                {
                    newPassword = newPassword.Substring(0, (int)options.Length);
                }
            }

            // this library returns password strength on a scale of 0 to 4
            var analysis = Zxcvbn.Zxcvbn.MatchPassword(newPassword);
            var passwordStrength = (double)analysis.Score / 4;
            Console.WriteLine($"Password stats -- calcTime: {analysis.CalcTime} crack time: {analysis.CrackTime} ctDisplay: {analysis.CrackTimeDisplay} entropy: {analysis.Entropy} score: {analysis.Score} strength: {passwordStrength}");
            var color = _utils.HexColorFromDouble(passwordStrength);
            var response = new ColorChangeResponse { HexColor = color, TextInput = newPassword + "|~|~|" + analysis.CrackTimeDisplay, Sentiment = passwordStrength };
            await SetColorNotifyObservers(response, false);
            return Json(response);
        }

        [HttpPost]
        public async Task<IActionResult> LightByText([FromBody]string text)
        {
            var analysis = await _utils.GetColorAndSentimentFromText(new List<string> { text });

            var response = new ColorChangeResponse {
                TextInput = text,
                Sentiment = analysis.First().Sentiment,
                HexColor = analysis.First().HexColor
            };

            await SetColorNotifyObservers(response, false);
            return Json(response);
        }

        [HttpPost]
        public async Task<IActionResult> BulkText([FromBody]List<string> texts)
        {
            var analysis = await _utils.BulkSentiment(texts);

            await _hubContext.Clients.All.SendAsync("Messages", _utils.GetColorFromTextAndSentiment(analysis));

            var response = new ColorChangeResponse { TextInput = "Bulk Analysis" };
            response.Sentiment = analysis.Average(i => i.Sentiment);
            response.HexColor = _utils.HexColorFromDouble(response.Sentiment);

            await _hubContext.Clients.All.SendAsync("BulkUpdate", new Tuple<string, double>(response.HexColor, response.Sentiment));
            await SetColorNotifyObservers(response, false, 5);
            return Json(response);
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private async Task SetColorNotifyObservers(ColorChangeResponse response, bool? notify = true, double? duration = 1)
        {
            await _lifxClient.SetState(new All(), new SentState { Color = $"#{response.HexColor}", Duration = (double)duration });
            if (notify == true)
            {
                await _hubContext.Clients.All.SendAsync("Messages", new List<ColorChangeResponse> { response });
            }
        }
    }
}
