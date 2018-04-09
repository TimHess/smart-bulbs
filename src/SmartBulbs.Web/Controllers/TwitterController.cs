using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using LifxIoT.Api;
using LifxIoT.Models;
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
        private LifxApi _lifxClient;
        private TwitterCredentials _twitterCreds;
        private Utils _utils;
        private string _twitterSearchTerm;

        public TwitterController(IConfiguration config, IOptionsSnapshot<TwitterCredentials> twitterCreds)
        {
            _lifxClient = new LifxApi(config.GetValue<string>("lifxKey"));
            _twitterCreds = twitterCreds.Value;
            _utils = new Utils(config.GetValue<string>("cognitiveServices:apiUrl"), config.GetValue<string>("cognitiveServices:apiKey"), new HttpClient());
            _twitterSearchTerm = config.GetValue<string>("twitterSearch");
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
                string searchTerm = _twitterSearchTerm;

                Search searchResponse =
                    await
                    (from search in ctx.Search
                     where search.Type == SearchType.Search &&
                           search.ResultType == ResultType.Mixed &&
                           search.Query == searchTerm &&
                           search.IncludeEntities == true &&
                           search.TweetMode == TweetMode.Extended &&
                           search.Count == 10 &&
                           search.SinceID == sinceId
                     select search)
                    .SingleOrDefaultAsync();

                if (!searchResponse.Statuses.Any())
                {
                    return Json(new TweetListWithSentiment
                    {
                        Tweets = new List<EnhancedTwitterStatus>()
                    });
                }

                sinceId = searchResponse.Statuses.Max(i => i.ID);
                var texts = searchResponse.Statuses.Select(t => t.FullText);
                var analyzed = await _utils.GetColorAndSentimentFromText(texts);
                var aggScore = analyzed.Average(r => r.Sentiment);

                var toReturn = new TweetListWithSentiment {
                    Tweets = new List<EnhancedTwitterStatus>(),
                    AggregateScore = aggScore,
                    AggregateColor = _utils.HexColorFromDouble(aggScore)
                };

                await _lifxClient.SetState(new All(), new SentState { Color = $"#{toReturn.AggregateColor}", Duration = 1, Power = "on" });

                foreach (var status in searchResponse.Statuses.Select((value, i) => new { i, value }))
                {
                    var s = status.value;
                    var analysis = analyzed.Find(a => a.TextInput == s.FullText);
                    toReturn.Tweets.Add(new EnhancedTwitterStatus
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