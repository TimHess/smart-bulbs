using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using LinqToTwitter;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SmartBulbs.Common;
using SmartBulbs.Web.Models;
using SmartBulbs.Web.Services;

namespace SmartBulbs.Web.Controllers
{
    public class TwitterController : Controller
    {
        private TwitterCredentials _twitterCreds;
        private Utils _utils;

        public TwitterController(IConfiguration config, IOptionsSnapshot<TwitterCredentials> twitterCreds)
        {
            _twitterCreds = twitterCreds.Value;
            _utils = new Utils(config.GetValue<string>("cognitiveServicesKey"), new HttpClient());
        }

        private ulong sinceId = 0;

        public async Task<IActionResult> Get()
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
                           search.ResultType == ResultType.Mixed &&
                           search.Query == searchTerm &&
                           search.IncludeEntities == true &&
                           search.TweetMode == TweetMode.Extended &&
                           search.Count == 2 &&
                           search.SinceID == sinceId
                     select search)
                    .SingleOrDefaultAsync();
                sinceId = searchResponse.Statuses.Max(i => i.ID);
                var texts = searchResponse.Statuses.Select(t => t.FullText);
                var analyzed = await _utils.GetColorAndSentimentFromText(texts);

                var toReturn = new List<EnhancedTwitterStatus>();
                foreach (var status in searchResponse.Statuses.Select((value, i) => new { i, value }))
                {
                    var s = status.value;
                    var analysis = analyzed.Find(a => a.TextInput == s.FullText);
                    toReturn.Add(new EnhancedTwitterStatus
                    {
                        FullText = s.FullText,
                        CreatedAt = s.CreatedAt,
                        User = s.User,
                        SentimentValue = analysis.Sentiment,
                        HexColor = analysis.HexColor
                    });
                }

                return Json(toReturn);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}