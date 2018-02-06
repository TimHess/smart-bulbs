using CredBulb.Models;
using Microsoft.AspNetCore.Mvc;
using Steeltoe.Security.DataProtection.CredHub;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;

namespace CredBulb.Controllers
{
    public class HomeController : Controller
    {
        private ICredHubClient credHubClient;

        public HomeController(ICredHubClient credHub)
        {
            credHubClient = credHub;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CredHubColorize()
        {
            // call credhub to generate a hex password
            var pwparams = new PasswordGenerationParameters { Length = 6 };
            var newColor = (await credHubClient.GenerateAsync<PasswordCredential>(new PasswordGenerationRequest("color", pwparams))).Value;
            //Color.FromArgb(red.)
            // call ifttt to colorize

            // return results
            return Json(newColor);
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
