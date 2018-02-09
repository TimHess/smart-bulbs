using CredBulb.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Steeltoe.Common.Http;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CredBulb.Controllers
{
    public class HomeController : Controller
    {
        private NewColorCommand colorCommand;
        private static HttpClient _httpClient;
        private JsonSerializerSettings _jsonSettings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
        private string _iftttUrl = "https://maker.ifttt.com/trigger/custom_light_up/with/key/***REMOVED***";

        public HomeController(NewColorCommand newColorCommand)
        {
            colorCommand = newColorCommand;
            _httpClient = new HttpClient();
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CredHubColorize()
        {
            // call credhub to generate a hex password
            var color = await colorCommand.ExecuteAsync();

            // call ifttt to colorize
            var response = await _httpClient.PostAsJsonAsync($"{_iftttUrl}", 
                new IftttWebhookPayload {
                    Value1 = color,
                    Value2 = "1" }, 
                _jsonSettings);

            // return results
            Thread.Sleep(1000);
            return Json(color);
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
