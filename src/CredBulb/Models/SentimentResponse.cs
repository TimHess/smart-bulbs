using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CredBulb.Models
{
    public class SentimentResponse
    {
        public List<Dictionary<string, string>> Documents { get; set; }
    }
}
