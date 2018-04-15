using LinqToTwitter;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SmartBulbs.Common;
using SmartBulbs.Web.Models;
using SmartBulbs.Web.Services;
using Steeltoe.CircuitBreaker.Hystrix;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace SmartBulbs.Web.Controllers
{
    public class TwitterController : Controller
    {
        private string _lifxKey;
        private TwitterCredentials _twitterCreds;
        private Utils _utils;
        private string _twitterSearchTerm;

        public TwitterController(IConfiguration config, IOptionsSnapshot<TwitterCredentials> twitterCreds)
        {
            _lifxKey = config.GetValue<string>("lifxKey");
            _twitterCreds = twitterCreds.Value;
            _utils = new Utils(config.GetValue<string>("cognitiveServices:apiUrl"), config.GetValue<string>("cognitiveServices:apiKey"), new HttpClient());
            _twitterSearchTerm = config.GetValue<string>("twitterSearch");
        }

        private static ulong sinceId = 0;

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
                           search.ResultType == ResultType.Recent &&
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
                var subset = searchResponse.Statuses.OrderBy(o => o.StatusID)/*.Take(2)*/;
                sinceId = subset.Max(i => i.StatusID);
                var texts = subset.Select(t => t.FullText);
                var analyzed = await _utils.GetColorAndSentimentFromText(texts);
                var aggScore = analyzed.Average(r => r.Sentiment);

                var toReturn = new TweetListWithSentiment {
                    Tweets = new List<EnhancedTwitterStatus>(),
                    AggregateScore = aggScore,
                    AggregateColor = _utils.HexColorFromDouble(aggScore)
                };
                var hystrixOptions = new HystrixCommandOptions(HystrixCommandKeyDefault.AsKey("SetColor"))
                {
                    GroupKey = HystrixCommandGroupKeyDefault.AsKey("SetColorGroup"),
                    ExecutionTimeoutEnabled = false
                };
                SetColorCommand command = new SetColorCommand(hystrixOptions, _lifxKey, toReturn.AggregateColor);
                await command.ExecuteAsync();

                foreach (var status in subset.Select((value, i) => new { i, value }))
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