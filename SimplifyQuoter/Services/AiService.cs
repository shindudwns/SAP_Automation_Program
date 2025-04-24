using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SimplifyQuoter.Services
{
    /// <summary>
    /// Wraps OpenAI Chat API calls for description & group classification.
    /// </summary>
    public class AiService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;

        public AiService()
        {
            _apiKey = ConfigurationManager.AppSettings["OpenAiApiKey"];
            if (string.IsNullOrWhiteSpace(_apiKey))
                throw new InvalidOperationException("OpenAI API key not configured.");

            _http = new HttpClient
            {
                BaseAddress = new Uri("https://api.openai.com/v1/")
            };
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        public async Task<string> DescribePartAsync(string part)
        {
            var payload = new
            {
                model = "gpt-3.5-turbo",
                messages = new object[] {
                  new { role = "system", content = "You are a concise parts catalog assistant." },
                  new { role = "user",   content = $"Provide a one-sentence description for the part: \"{part}\"" }
                }
            };
            var resp = await _http.PostAsync(
                "chat/completions",
                new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
            );
            resp.EnsureSuccessStatusCode();
            var root = JObject.Parse(await resp.Content.ReadAsStringAsync());
            return root["choices"]?[0]?["message"]?["content"]?.ToString().Trim()
                   ?? string.Empty;
        }

        public async Task<string> ClassifyGroupAsync(string part)
        {
            var payload = new
            {
                model = "gpt-3.5-turbo",
                messages = new object[] {
                  new { role = "system", content = "You classify parts into Electronics, Mechanical, Consumables, or Other." },
                  new { role = "user",   content = $"Which of these groups best fits \"{part}\"? Return only the group name." }
                }
            };
            var resp = await _http.PostAsync(
                "chat/completions",
                new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
            );
            resp.EnsureSuccessStatusCode();
            var root = JObject.Parse(await resp.Content.ReadAsStringAsync());
            return root["choices"]?[0]?["message"]?["content"]?.ToString().Trim()
                   ?? "Other";
        }
    }
}
