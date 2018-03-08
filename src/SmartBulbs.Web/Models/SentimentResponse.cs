using System.Collections.Generic;

namespace SmartBulbs.Web.Models
{
    public class SentimentResponse
    {
        public List<Dictionary<string, string>> Documents { get; set; }
    }
}
