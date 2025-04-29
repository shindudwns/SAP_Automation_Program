using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SimplifyQuoter.Services
{
    public class AiEnrichmentService
    {
        private readonly DatabaseService _db;
        private readonly SemaphoreSlim _throttle = new SemaphoreSlim(1, 1);
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly string _modelName;
        private readonly TimeSpan[] _backoffs =
        {
            TimeSpan.FromMilliseconds(500),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
        };

        public AiEnrichmentService(DatabaseService db)
        {
            _db = db;
            _apiKey = ConfigurationManager.AppSettings["OpenAI:ApiKey"];
            _modelName = ConfigurationManager.AppSettings["OpenAI:Model"]
                         ?? "gpt-3.5-turbo";

            if (string.IsNullOrWhiteSpace(_apiKey))
                throw new InvalidOperationException(
                    "Missing OpenAI API key in App.config under <appSettings> key: OpenAI:ApiKey");

            _http = new HttpClient();
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        /// <summary>
        /// Enrich *just* descriptions (legacy).
        /// </summary>
        public async Task EnrichMissingAsync(IEnumerable<string> allCodes)
        {
            var distinct = allCodes
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var known = _db.GetKnownPartCodes(distinct).ToList();
            var missing = distinct.Except(known, StringComparer.OrdinalIgnoreCase).ToList();

            // chunk 20
            for (int i = 0; i < missing.Count; i += 20)
            {
                var batch = missing.GetRange(i, Math.Min(20, missing.Count - i));
                await CallOpenAiWithRetryAsync(batch);
            }
        }

        /// <summary>
        /// Enrich both description & item_group, using full context.
        /// </summary>
        public async Task EnrichMissingWithContextAsync(IEnumerable<PartContext> parts)
        {
            var list = parts
                .Where(p => !string.IsNullOrWhiteSpace(p.Code))
                .Distinct(new PartContextComparer())
                .ToList();

            var codes = list.Select(p => p.Code).ToList();
            var known = _db.GetKnownPartCodes(codes);
            var missing = list
                .Where(p => !known.Contains(p.Code))
                .ToList();

            for (int i = 0; i < missing.Count; i += 20)
            {
                var batch = missing.GetRange(i, Math.Min(20, missing.Count - i));
                await CallOpenAiContextWithRetryAsync(batch);
            }
        }

        private async Task CallOpenAiWithRetryAsync(List<string> batch)
        {
            await _throttle.WaitAsync();
            try
            {
                for (int attempt = 0; attempt < _backoffs.Length; attempt++)
                {
                    try
                    {
                        await CallOpenAiAsync(batch);
                        return;
                    }
                    catch (HttpRequestException ex) when (ex.Message.Contains("429") && attempt < _backoffs.Length - 1)
                    {
                        await Task.Delay(_backoffs[attempt]);
                    }
                }
            }
            finally
            {
                _throttle.Release();
            }
        }

        private async Task CallOpenAiContextWithRetryAsync(List<PartContext> batch)
        {
            await _throttle.WaitAsync();
            try
            {
                for (int attempt = 0; attempt < _backoffs.Length; attempt++)
                {
                    try
                    {
                        await CallOpenAiWithContextAsync(batch);
                        return;
                    }
                    catch (HttpRequestException ex) when (ex.Message.Contains("429") && attempt < _backoffs.Length - 1)
                    {
                        await Task.Delay(_backoffs[attempt]);
                    }
                }
            }
            finally
            {
                _throttle.Release();
            }
        }

        private async Task CallOpenAiAsync(List<string> batch)
        {
            // legacy: only code → description
            var partsList = string.Join(", ", batch.Select(c => $"\"{c}\""));
            var userContent =
                "Provide a JSON array of objects with fields:\n" +
                "  code: string,\n" +
                "  description: ≤10-word summary\n" +
                "Parts:\n" + partsList;

            var bodyObj = new
            {
                model = _modelName,
                messages = new[]
                {
                    new { role = "system", content = "You are an expert parts-classifier." },
                    new { role = "user",   content = userContent }
                }
            };

            var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
            {
                Content = new StringContent(
                    JsonConvert.SerializeObject(bodyObj),
                    Encoding.UTF8,
                    "application/json")
            };

            var rsp = await _http.SendAsync(req);
            rsp.EnsureSuccessStatusCode();

            var json = await rsp.Content.ReadAsStringAsync();
            dynamic parsed = JsonConvert.DeserializeObject(json);
            string content = ((string)parsed.choices[0].message.content ?? "").Trim();

            // extract JSON array
            int start = content.IndexOf('['), end = content.LastIndexOf(']');
            if (start >= 0 && end > start)
                content = content.Substring(start, end - start + 1);

            List<Part> results;
            try
            {
                results = JsonConvert.DeserializeObject<List<Part>>(content);
            }
            catch (JsonReaderException ex)
            {
                Debug.WriteLine($"Failed to parse AI-desc: {ex}\n{content}");
                return;
            }

            foreach (var p in results)
            {
                using (var d = new DatabaseService())
                    d.UpsertPart(p.code, p.description, p.item_group, p.is_manual);
            }
        }

        private async Task CallOpenAiWithContextAsync(List<PartContext> batch)
        {
            var jsonInputs = batch
                .Select(p =>
                    $"{{\"code\":\"{p.Code}\",\"brand\":\"{p.Brand}\",\"vendor\":\"{p.Vendor}\"}}");
            var inputArray = "[" + string.Join(",", jsonInputs) + "]";
            Debug.WriteLine(inputArray);
            var userBuilder = new StringBuilder()
                .AppendLine("You are an expert parts-classifier.")
                .AppendLine("For each item below, given its code, brand and vendor,")
                .AppendLine("output a JSON array of objects with fields:")
                .AppendLine("  code: string,")
                .AppendLine("  description: ≤20-word summary including details, such as length, voltage etc,")
                .AppendLine("  item_group: one of [")
                // full item group list here:
                .AppendLine("    Automation and Controls, BRASS FITTINGS, Bearings and Power Transmission,")
                .AppendLine("    Bolts, Nuts, Washers, Charge, ETC, Electrical Components, Facility Maintenance,")
                .AppendLine("    Fasteners and Hardware, HVAC and Refrigeration, IT and Office Supplies,")
                .AppendLine("    Lighting and Electrical Fixtures, Lubricants and Chemicals, Material Handling,")
                .AppendLine("    Packaging and Shipping, Plumbing and Fluid Handling, Pneumatics and Hydraulics,")
                .AppendLine("    Precision Measuring Tools, SERVICE, STEEL FITTINGS, Safety and PPE, Surplus,")
                .AppendLine("    Tools and Equipment, Welding and Soldering")
                .AppendLine("  ], if you are uncertain, choose ETC.")
                .AppendLine("Items:")
                .Append(inputArray);

            var bodyObj = new
            {
                model = _modelName,
                messages = new[]
                {
                    new { role = "system", content = userBuilder.ToString() }
                }
            };

            var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
            {
                Content = new StringContent(
                    JsonConvert.SerializeObject(bodyObj),
                    Encoding.UTF8,
                    "application/json")
            };

            var rsp = await _http.SendAsync(req);
            rsp.EnsureSuccessStatusCode();

            var json = await rsp.Content.ReadAsStringAsync();
            dynamic parsed = JsonConvert.DeserializeObject(json);
            string content = ((string)parsed.choices[0].message.content ?? "").Trim();

            // extract JSON array
            int start = content.IndexOf('['), end = content.LastIndexOf(']');
            if (start >= 0 && end > start)
                content = content.Substring(start, end - start + 1);

            List<Part> results;
            try
            {
                results = JsonConvert.DeserializeObject<List<Part>>(content);
            }
            catch (JsonReaderException ex)
            {
                Debug.WriteLine($"Failed to parse AI-context: {ex}\n{content}");
                return;
            }

            foreach (var p in results)
            {
                using (var d = new DatabaseService())
                    d.UpsertPart(
                        p.code,
                        p.description,
                        p.item_group,
                        p.is_manual);
            }
        }

        private class Part
        {
            public string code { get; set; }
            public string description { get; set; }
            public string item_group { get; set; }
            public bool is_manual { get; set; }
        }

        /// <summary>
        /// Distinct-by-code comparer for PartContext.
        /// </summary>
        private class PartContextComparer : IEqualityComparer<PartContext>
        {
            public bool Equals(PartContext x, PartContext y)
            {
                return string.Equals(x.Code, y.Code,
                                     StringComparison.OrdinalIgnoreCase);
            }
            public int GetHashCode(PartContext obj)
            {
                return obj.Code?.ToLowerInvariant().GetHashCode() ?? 0;
            }
        }
    }
}
