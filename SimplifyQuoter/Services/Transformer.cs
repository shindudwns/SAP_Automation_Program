using System.Text.RegularExpressions;

namespace SimplifyQuoter.Services
{
    /// <summary>
    /// Converts raw duration strings (e.g. "2개월", "3주") into the final free-text ("8 WEEK ERO", "3 WEEK ERO").
    /// </summary>
    public static class Transformer
    {
        public static string ConvertDurationToFreeText(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            // Match a number + unit ("주" or "개월")
            var m = Regex.Match(raw.Trim(), @"^(?<n>\d+)\s*(?<unit>주|개월)$");
            if (!m.Success)
                return raw;

            int n = int.Parse(m.Groups["n"].Value);
            bool isMonth = m.Groups["unit"].Value == "개월";

            // 1 month = 4 weeks
            int weeks = isMonth ? n * 4 : n;
            return $"{weeks} WEEK ERO";
        }
    }
}
