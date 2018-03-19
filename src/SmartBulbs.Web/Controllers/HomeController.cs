using LifxIoT.Api;
using LifxIoT.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SmartBulbs.Common;
using SmartBulbs.Web.Hubs;
using SmartBulbs.Web.Models;
using SmartBulbs.Web.Services;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace SmartBulbs.Web.Controllers
{
    public class HomeController : Controller
    {
        private NewPasswordCommand _colorCommand;
        private static HttpClient _httpClient;
        private JsonSerializerSettings _jsonSettings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
        private IHubContext<ObservationHub> _hubContext;
        private LifxApi _lifxClient;
        private Utils _utils;

        public HomeController(IConfiguration config, NewPasswordCommand newColorCommand, IHubContext<ObservationHub> hubContext)
        {
            _colorCommand = newColorCommand;
            _httpClient = new HttpClient();
            _hubContext = hubContext;
            _lifxClient = new LifxApi(config.GetValue<string>("lifxKey"));
            _utils = new Utils(config.GetValue<string>("cognitiveServicesKey"), _httpClient);
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
            var newPassword = await _colorCommand.ExecuteAsync();

            // hash the password and shift the bits to convert to RGB
            int hash = newPassword.GetHashCode();
            string r = ((hash & 0xFF0000) >> 16).ToString("X2");
            string g = ((hash & 0x00FF00) >> 8).ToString("X2");
            string b = (hash & 0x0000FF).ToString("X2");

            var response = new ColorChangeResponse { HexColor = r + g + b, TextInput = newPassword };

            await SetColorNotifyObservers(response);
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

            await SetColorNotifyObservers(response);
            return Json(response);
        }

        [HttpPost]
        public async Task<IActionResult> BulkText([FromBody]List<string> texts)
        {
            var analysis = await _utils.BulkSentiment(texts);

            await _hubContext.Clients.All.SendAsync("Messages", _utils.GetColorFromTextAndSentiment(analysis));

            var response = new ColorChangeResponse { TextInput = "Bulk Analysis" };
            response.Sentiment = analysis.Average(i => i.Sentiment);
            response.HexColor = _utils.HexColorFromDecimal(response.Sentiment);

            await SetColorNotifyObservers(response, false);
            return Json(response);
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private async Task SetColorNotifyObservers(ColorChangeResponse response, bool? notify = true)
        {
            await _lifxClient.SetState(new All(), new SentState { Color = $"#{response.HexColor}", Duration = 1 });
            if (notify == true)
            {
                await _hubContext.Clients.All.SendAsync("Messages", new List<ColorChangeResponse> { response });
            }
        }
    }
}
