// File: Services/ServiceLayer/ItemService.cs
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
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

        /// <summary>
        /// 1) POST the Item header
        /// 2) PATCH the Item to add preferred vendor + UoM entries in one shot
        /// </summary>
        public async Task CreateOrUpdateAsync(ItemDto dto)
        {
            // 1) Create the header
            var headerPayload = new
            {
                ItemCode = dto.ItemCode,
                ItemName = dto.ItemName,
                ForeignName = dto.FrgnName,
                ItemsGroupCode = dto.ItmsGrpCod,
                PurchaseItem = "tYES",
                SalesItem = "tYES",
                InventoryItem = "tYES"
            };
            var jsonHeader = JsonConvert.SerializeObject(
                headerPayload,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }
            );
            Debug.WriteLine("📤 SL CreateItem header payload:");
            Debug.WriteLine(jsonHeader);


            // make sure our cookies are applied (same snippet you've been using)
            var sl = _client as ServiceLayerClient;
            if (sl != null)
            {
                var baseUri = sl.HttpClient.BaseAddress;
                var auth = new Uri(baseUri.GetLeftPart(UriPartial.Authority));
                var cookies = sl.Cookies.GetCookies(auth);
                var sess = cookies["B1SESSION"]?.Value;
                var route = cookies["ROUTEID"]?.Value;

                Debug.WriteLine("📡 SL Cookies:");
                foreach (Cookie c in cookies)
                    Debug.WriteLine($" - {c.Name}={c.Value}");

                if (!string.IsNullOrEmpty(sess))
                {
                    sl.HttpClient.DefaultRequestHeaders.Remove("Cookie");
                    sl.HttpClient.DefaultRequestHeaders.Add(
                        "Cookie",
                        $"B1SESSION={sess}; ROUTEID={route}"
                    );
                }
            }

            using (var content = new StringContent(jsonHeader, Encoding.UTF8, "application/json"))
            {
                var resp = await _client.HttpClient.PostAsync("Items", content).ConfigureAwait(false);
                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                Debug.WriteLine($"📥 SL CreateItem header response: {(int)resp.StatusCode} {resp.ReasonPhrase}");
                Debug.WriteLine(body);
                resp.EnsureSuccessStatusCode();
            }

            // 2) PATCH in both child collections in one go
            // Build one payload that includes both arrays
            var patchPayload = new
            {
                ItemPreferredVendors = string.IsNullOrEmpty(dto.CardCode)
            ? null
            : new[] {
                new {
                    PreferredVendor = dto.CardCode,   // ← use this field name
                    DefaultVendor   = "tYES"
                }
              },

                ItemUnitOfMeasurementCollection = new[] {
            new { UoMEntry   = dto.PurchasingUoM, DefaultUoM = "tYES" },
            new { UoMEntry   = dto.SalesUoM,      DefaultUoM = "tYES" },
            new { UoMEntry   = dto.InventoryUoM,    DefaultUoM = "tYES" }
        }
            };

            var jsonPatch = JsonConvert.SerializeObject(
                patchPayload,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }
            );
            Debug.WriteLine("📤 SL PATCH Item children payload:");
            Debug.WriteLine(jsonPatch);

            var req = new HttpRequestMessage(
                new HttpMethod("PATCH"),
                $"Items('{dto.ItemCode}')"
            )
            {
                Content = new StringContent(jsonPatch, Encoding.UTF8, "application/json")
            };

            using (var resp = await _client.HttpClient.SendAsync(req).ConfigureAwait(false))
            {
                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                Debug.WriteLine($"📥 SL PATCH Item children response: {(int)resp.StatusCode} {resp.ReasonPhrase}");
                Debug.WriteLine(body);
                resp.EnsureSuccessStatusCode();
            }
        }

    }
}
