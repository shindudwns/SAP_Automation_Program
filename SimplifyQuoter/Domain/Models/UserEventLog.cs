using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimplifyQuoter.Models
{
    public class UserEventLog
    {
        public long Id { get; set; }
        public DateTime Ts { get; set; }
        public string UserId { get; set; }   // null 가능하지만 C# 7.3에서는 ? 표기 없음
        public string Event { get; set; }
        public string MetaJson { get; set; }
        public string Machine { get; set; }
        public string IpAddress { get; set; }


        // ▼ GPT 전용(없으면 null)
        public int? GptPromptTokens { get; set; }
        public int? GptCompletionTokens { get; set; }
        public int? GptTotalTokens { get; set; }
        public string GptModel { get; set; }
        public string GptFeature { get; set; }
        public string GptPartCode { get; set; }
        public int? GptItems { get; set; }
    }
}
