using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CredBulb.Models
{
    public class IftttWebhookPayload
    {
        /// <summary>
        /// Color
        /// </summary>
        public string Value1 { get; set; }

        /// <summary>
        /// Brightness
        /// </summary>
        public string Value2 { get; set; }

        public string Value3 { get; set; } = string.Empty;
    }
}
