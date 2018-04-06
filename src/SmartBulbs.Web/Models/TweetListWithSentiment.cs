using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SmartBulbs.Web.Models
{
    public class TweetListWithSentiment
    {
        public double AggregateScore { get; set; }

        public string AggregateColor { get; set; }

        public List<EnhancedTwitterStatus> Tweets { get; set; }
    }
}
