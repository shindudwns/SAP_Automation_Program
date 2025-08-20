using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimplifyQuoter.Models
{
    public class GptDailyRow
    {
        public DateTime Day { get; set; }
        public string UserId { get; set; }

        public int Logins { get; set; }
        public int Tokens { get; set; }
        public int Calls { get; set; }
    }

    public class GptTopUserRow
    {
        public string UserId { get; set; }
        public int Tokens { get; set; }
        public int Calls { get; set; }
        public int AvgTokens { get; set; } // Tokens / Calls (반올림)
    }

    public class GptFeatureRow
    {
        public string Feature { get; set; }
        public int Tokens { get; set; }
        public int Calls { get; set; }
    }
}
