using LinqToTwitter;

namespace CredBulb.Models
{
    public class EnhancedTwitterStatus : Status
    {
        public double SentimentValue { get; set; }

        public string HexColor { get; set; }
    }
}
