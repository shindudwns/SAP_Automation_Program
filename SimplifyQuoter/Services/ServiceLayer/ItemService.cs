// File: Services/ServiceLayer/ItemService.cs
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SimplifyQuoter.Services.ServiceLayer;
using SimplifyQuoter.Services.ServiceLayer.Dtos;

namespace SimplifyQuoter.Services.ServiceLayer
{
    public class ItemService
    {
        private readonly IServiceLayerClient _client;

        public ItemService(IServiceLayerClient client)
        {
            _client = client;
        }

        public async Task CreateOrUpdateAsync(ItemDto dto)
        {
            // 1) Serialize and log the outgoing JSON
            var json = JsonConvert.SerializeObject(dto);
            Debug.WriteLine("📤 SL CreateItem payload:");
            Debug.WriteLine(json);

            // 2) Dump cookies from ServiceLayerClient
            if (_client is ServiceLayerClient slClient)
            {
                var baseUri = slClient.HttpClient.BaseAddress;
                var authorityUri = new Uri(baseUri.GetLeftPart(UriPartial.Authority));

                Debug.WriteLine("📡 SL Cookies at authority root:");
                foreach (Cookie c in slClient.Cookies.GetCookies(authorityUri))
                    Debug.WriteLine($" - {c.Name}={c.Value}");

                Debug.WriteLine("📡 SL Cookies at BaseAddress path:");
                foreach (Cookie c in slClient.Cookies.GetCookies(baseUri))
                    Debug.WriteLine($" - {c.Name}={c.Value}");

                // 3) Fallback: manually set the Cookie header if B1SESSION is present
                var sessionCookie = slClient.Cookies.GetCookies(authorityUri)["B1SESSION"]?.Value;
                var routeCookie = slClient.Cookies.GetCookies(authorityUri)["ROUTEID"]?.Value;
                if (!string.IsNullOrEmpty(sessionCookie))
                {
                    slClient.HttpClient.DefaultRequestHeaders.Remove("Cookie");
                    slClient.HttpClient.DefaultRequestHeaders.Add(
                        "Cookie",
                        $"B1SESSION={sessionCookie}; ROUTEID={routeCookie}"
                    );
                    Debug.WriteLine(
                        "📡 SL Manually set Cookie header: " +
                        slClient.HttpClient
                                .DefaultRequestHeaders
                                .GetValues("Cookie")
                                .First()
                    );
                }
            }

            // 4) Send the request
            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            {
                var resp = await _client.HttpClient.PostAsync("Items", content);

                // 5) Read & log the response
                var body = await resp.Content.ReadAsStringAsync();
                Debug.WriteLine($"📥 SL CreateItem response: {(int)resp.StatusCode} {resp.ReasonPhrase}");
                Debug.WriteLine(body);

                resp.EnsureSuccessStatusCode();
            }
        }
    }
}
