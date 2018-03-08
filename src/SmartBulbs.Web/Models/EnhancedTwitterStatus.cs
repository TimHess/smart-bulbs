using LinqToTwitter;

namespace SmartBulbs.Web.Models
{
    public class EnhancedTwitterStatus : Status
    {
        public double SentimentValue { get; set; }

        public string HexColor { get; set; }
    }
}
