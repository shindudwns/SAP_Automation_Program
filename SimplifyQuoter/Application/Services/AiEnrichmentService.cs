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

using System.Text.RegularExpressions;


namespace SimplifyQuoter.Services
{

    public class AiEnrichmentService : IDisposable
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
        
        private static readonly Dictionary<string, int> GroupLookup =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "Surplus", 100 },
            { "Bearings and Power Transmission", 101 },
            { "Electrical Components", 102 },
            { "HVAC and Refrigeration", 103 },
            { "Plumbing and Fluid Handling", 104 },
            { "Tools and Equipment", 105 },
            { "Safety and PPE", 106 },
            { "Material Handling", 107 },
            { "Fasteners and Hardware", 108 },
            { "Lubricants and Chemicals", 109 },
            { "Lighting and Electrical Fixtures", 110 },
            { "Facility Maintenance", 111 },
            { "Welding and Soldering", 112 },
            { "Packaging and Shipping", 113 },
            { "IT and Office Supplies", 114 },
            { "Automation and Controls", 115 },
            { "Pneumatics and Hydraulics", 116 },
            { "Precision Measuring Tools", 117 },
            { "BRASS FITTINGS", 118 },
            { "STEEL FITTINGS", 119 },
            { "Bolts, Nuts, Washers", 120 },
            { "ETC", 121 },
            { "Charge", 122 },
            { "SERVICE", 123 },
        };
        public AiEnrichmentService()
        {
            _apiKey = ConfigurationManager.AppSettings["OpenAI:ApiKey"]
                     ?? throw new InvalidOperationException(
                         "Missing OpenAI:ApiKey in App.config");
            _modelName = ConfigurationManager.AppSettings["OpenAI:Model"]
                         ?? "gpt-4o-mini";

            _http = new HttpClient();
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);
        }


        public AiEnrichmentService(DatabaseService db)
        {
            _db = db;
            _apiKey = ConfigurationManager.AppSettings["OpenAI:ApiKey"];
            _modelName = ConfigurationManager.AppSettings["OpenAI:Model"]
                         ?? "gpt-4o-mini";

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
                "  description: ≤10-word summary in english\n" +
                "Parts:\n" + partsList;

            var bodyObj = new
            {
                model = _modelName,
                messages = new[]
                {
                    new { role = "system", content = "You are an American expert parts-classifier. You must translate any non-English text to English first before summarizing. Use only English in your response." },
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
                    $"{{\"code\":\"{p.Code}\",\"brand\":\"{p.Brand}\"}}");
            var inputArray = "[" + string.Join(",", jsonInputs) + "]";
            Debug.WriteLine(inputArray);
            var userBuilder = new StringBuilder()
                .AppendLine("You are an American expert parts-classifier. ")
                .AppendLine("For each item below, given its part number (code) and brand,")
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

        /// <summary>
        /// Generate a ≤20-word summary for the specified part code.
        /// </summary>
        public async Task<string> GeneratePartSummaryAsync(string code, string brand)
        {
            if (string.IsNullOrWhiteSpace(code))
                return string.Empty;

            // Prompt for concise summary
            var prompt = $@"
                You are an expert in industrial/electromechanical parts. Given only a Part number and Brand, output a very short, all-caps product description containing only the core details (e.g., series/family, function, channel count, voltage). Omit any packaging, bus compatibility, or ancillary features.

                Examples:

                #1
                Part number: 1FT6044-4AF71-3EB2  
                Brand: SIEMENS  
                SERVO MOTOR, 1FT6 SIMOTICS S SYNCHRONOUS SERVOMOTOR, 3 PHASE, 3 AMP, 3000 RPM, 5 NM

                #2
                Part number: HR705-2PL-24VDC  
                Brand: Kacon  
                ELECTROMECHANICAL RELAY, ICE CUBE TYPE, DPDT (2NO + 2NC) CONTACT CONFIGURATION, 24 V DC COIL VOLTAGE, 5 A CONTACT RATING, 250 V AC / 125 V DC SWITCHING VOLTAGE

                #3
                Part number: WF22-55  
                Brand: MISUMI  
                ROUND WIRE SPRING, 0.1 KG

                ---

                Now generate for:


                Part number: ""{code}"" 
                Brand: ""{brand}""";

            return await SendWithRetryAsync(prompt);
        }

        /// <summary>
        /// Selects the best item group code given part & brand. Defaults to ETC (121).
        /// </summary>
        public async Task<int> DetermineItemGroupCodeAsync(string code, string brand)
        {
            if (string.IsNullOrWhiteSpace(code))
                return GroupLookup["ETC"];

            // Build a JSON-style list of choices
            var choices = string.Join(", ", GroupLookup.Keys.Select(k => $"\"{k}\""));
            var prompt = new StringBuilder()
                .AppendLine($"Given part number \"{code}\" and brand \"{brand}\",")
                .AppendLine("select exactly one item group from the list below:")
                .AppendLine($"[{choices}]")
                .AppendLine("Respond with the group name only; if uncertain, choose \"ETC\".")
                .ToString();

            var aiResult = await SendWithRetryAsync(prompt);
            var groupName = aiResult.Trim().Trim('\"');

            if (GroupLookup.TryGetValue(groupName, out var grpCode))
                return grpCode;

            return GroupLookup["ETC"];
        }

        //
        // ▼▼▼ 여기에 추가: DetermineItemGroupCodeAsync(...) 바로 아래, SendWithRetryAsync(...) 위 ▼▼▼

        public async Task<string> ToEnglishKeywordsAsync(string rawDescription, bool uppercase = true)
        {
            if (string.IsNullOrWhiteSpace(rawDescription))
                return string.Empty;

            // ✅ 항상 GPT로 돌린다 (한글 감지 분기 제거)
            var prompt =
                "Extract ONLY core, product-related KEYWORDS from the text below. " +
                "The input may be Korean or English. " +
                "Rules: output MUST be English ONLY, comma-separated, NO sentences, NO period at the end, " +
                "no filler words, no stopwords, focus on specs (e.g., size, voltage, rating, model series).\n\n" +
                $"\"{rawDescription}\"";

            string result;
            try
            {
                result = await SendWithRetryAsync(prompt);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ToEnglishKeywordsAsync failed: {ex}");
                result = rawDescription; // 실패 시 원문 보존
            }

            if (string.IsNullOrWhiteSpace(result))
                return string.Empty;

            // 정규화
            var normalized = result.Trim()
                                   .Trim('.')          // 끝 마침표 제거
                                   .Replace(" ,", ",")
                                   .Replace(",  ", ", ")
                                   .Replace("  ", " ");

            // 🚧 방어: 혹시라도 한국어가 섞이면 제거
            // (한글 범위 제거: 가~힣, ㅏ~ㅣ, ㄱ~ㅎ)
            normalized = Regex.Replace(normalized, @"[가-힣\u3130-\u318F\uAC00-\uD7A3]+", string.Empty)
                              .Replace(" ,", ",")
                              .Replace(", ,", ",")
                              .Trim(' ', ',');

            return uppercase ? normalized.ToUpperInvariant() : normalized;
        }

        // ▲▲▲ 여기까지 추가 ▲▲▲



        /// <summary>
        /// Sends a user prompt via ChatCompletion with retry/backoff.
        /// </summary>
        public async Task<string> SendWithRetryAsync(string userPrompt)
        {
            await _throttle.WaitAsync();
            try
            {
                for (int i = 0; i < _backoffs.Length; i++)
                {
                    try
                    {
                        return await CallChatCompletionAsync(userPrompt);
                    }
                    catch (HttpRequestException ex)
                    {
                        // on 429 retry
                        if (ex.Message.Contains("429") && i < _backoffs.Length - 1)
                        {
                            await Task.Delay(_backoffs[i]);
                            continue;
                        }
                        throw;
                    }
                }
            }
            finally
            {
                _throttle.Release();
            }

            return string.Empty; // fallback
        }

        /// <summary>
        /// One-off ChatCompletion call, returns the assistant’s reply text.
        /// </summary>
        private async Task<string> CallChatCompletionAsync(string userContent)
        {
            var body = new
            {
                model = _modelName,
                messages = new[] {
                    new { role = "system", content = "You are an American expert parts-classifier. You must translate any non-English text to English first before summarizing. Use only English in your response." },
                    new { role = "user",   content = userContent             }
                }
            };

            var req = new HttpRequestMessage(
                HttpMethod.Post,
                "https://api.openai.com/v1/chat/completions")
            {
                Content = new StringContent(
                    JsonConvert.SerializeObject(body),
                    Encoding.UTF8,
                    "application/json")
            };

            var resp = await _http.SendAsync(req);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            dynamic parsed = JsonConvert.DeserializeObject(json);
            string content = ((string)parsed.choices[0].message.content ?? "")
                             .Trim();

            return content;
        }

        public void Dispose()
        {
            _http?.Dispose();
            _throttle?.Dispose();
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
