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
        private readonly TimeSpan[] _backoffs = new[]
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

        public async Task EnrichMissingAsync(IEnumerable<string> allCodes)
        {
            var distinct = allCodes
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var known = _db.GetKnownPartCodes(distinct).ToList();
            var missing = distinct.Except(known, StringComparer.OrdinalIgnoreCase).ToList();

            for (int i = 0; i < missing.Count; i += 20)
            {
                var batch = missing.GetRange(i, Math.Min(20, missing.Count - i));
                await CallOpenAiWithRetryAsync(batch);
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
                    catch (HttpRequestException ex) when (ex.Message.Contains("429"))
                    {
                        if (attempt == _backoffs.Length - 1) break;
                        Debug.WriteLine($"Rate limit, backing off {_backoffs[attempt]}");
                        await Task.Delay(_backoffs[attempt]);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"OpenAI call failed: {ex}");
                        return;
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
            // build your prompt as before…
            var partsList = string.Join(", ", batch.Select(c => "\"" + c + "\""));
            var userContent = new StringBuilder()
                .AppendLine("Provide a JSON array of objects, each with fields:")
                .AppendLine("  code: string")
                .AppendLine("  description: <=10-word summary")
                .AppendLine("  item_group: one of [Pump, Valve, Motor, Sensor, Other]")
                .AppendLine("Parts:")
                .Append(partsList)
                .ToString();

            var bodyObj = new
            {
                model = "gpt-4",
                messages = new[]
                {
            new { role = "system", content = "You are an expert parts-classifier." },
            new { role = "user",   content = userContent }
        }
            };

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://api.openai.com/v1/chat/completions"
            )
            {
                Content = new StringContent(
                    JsonConvert.SerializeObject(bodyObj),
                    Encoding.UTF8,
                    "application/json"
                )
            };

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            // get the assistant's raw response
            var rspJson = await response.Content.ReadAsStringAsync();
            dynamic parsed = JsonConvert.DeserializeObject(rspJson);
            string content = ((string)parsed.choices[0].message.content) ?? string.Empty;

            // ** SANITIZE JSON ** 
            content = content.Trim();

            // grab only what's between the first '[' and the last ']'
            int start = content.IndexOf('[');
            int end = content.LastIndexOf(']');
            if (start >= 0 && end > start)
                content = content.Substring(start, end - start + 1);

            List<Part> results;
            try
            {
                results = JsonConvert.DeserializeObject<List<Part>>(content);
            }
            catch (Newtonsoft.Json.JsonReaderException ex)
            {
                // log the bad payload for inspection
                Debug.WriteLine($"Failed to deserialize parts list: {ex}\n\nPayload:\n{content}");
                return;
            }

            // upsert each into your part table
            foreach (var p in results)
            {
                using (var db2 = new DatabaseService())
                {
                    db2.UpsertPart(
                        p.code,
                        p.description,
                        p.item_group,
                        p.is_manual
                    );
                }
            }
        }


        private class Part
        {
            public string code { get; set; }
            public string description { get; set; }
            public string item_group { get; set; }
            public bool is_manual { get; set; }
        }
    }
}
