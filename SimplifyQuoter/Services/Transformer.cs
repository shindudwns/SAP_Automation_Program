// Services/Transformer.cs

using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SimplifyQuoter.Services
{
    /// <summary>
    /// Converts Korean durations (e.g. "2개월", "3주") to "WEEK ERO"
    /// and asynchronously fetches/​generates part descriptions via AI (with caching).
    /// </summary>
    public static class Transformer
    {
        public static string ConvertDurationToFreeText(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            var m = Regex.Match(raw.Trim(), @"^(?<n>\d+)\s*(?<unit>주|개월)$");
            if (!m.Success)
                return raw.Trim();

            int n = int.Parse(m.Groups["n"].Value);
            bool isMonth = m.Groups["unit"].Value == "개월";
            int weeks = isMonth ? n * 4 : n;
            return $"{weeks} WEEK ERO";
        }

        /// <summary>
        /// Return a cached description for this code—or generate it via AI if missing.
        /// Any errors in the AI call are caught and result in an empty string fallback.
        /// </summary>
        public static Task<string> GetDescriptionAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return Task.FromResult(string.Empty);

            return GetDescriptionInternalAsync(code.Trim());
        }

        private static async Task<string> GetDescriptionInternalAsync(string code)
        {
            // 1) cache lookup
            string desc;
            try
            {
                using (var db1 = new DatabaseService())
                    desc = db1.GetDescription(code);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DatabaseService.GetDescription failed for {code}: {ex}");
                desc = null;
            }

            if (!string.IsNullOrEmpty(desc))
                return desc;

            // 2) kick off AI enrichment—but swallow any exceptions
            try
            {
                using (var db2 = new DatabaseService())
                {
                    var ai = new AiEnrichmentService(db2);
                    await ai.EnrichMissingAsync(new[] { code });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AiEnrichmentService failed for {code}: {ex}");
            }

            // 3) re-read from cache
            try
            {
                using (var db3 = new DatabaseService())
                {
                    desc = db3.GetDescription(code);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DatabaseService.GetDescription (post-AI) failed for {code}: {ex}");
                desc = null;
            }

            return desc ?? string.Empty;
        }
    }
}
