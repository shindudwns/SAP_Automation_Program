// File: Services/ServiceLayer/ItemService.cs
using System;
using System.Diagnostics;
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
        /// 1) POST the Item header (creating a new item).
        ///    If it already exists, this will return 400 Bad Request with "Item code 'X' already exists".
        /// </summary>
        public async Task CreateOrUpdateAsync(ItemDto dto)
        {
            var payload = new
            {
                ItemCode = dto.ItemCode,
                ItemName = dto.ItemName,
                ForeignName = dto.FrgnName,
                ItemsGroupCode = dto.ItmsGrpCod,
                PurchaseItem = "tYES",
                SalesItem = "tYES",
                InventoryItem = "tYES",
                PurchaseUnit = dto.PurchaseUnit,
                SalesUnit = dto.SalesUnit,
                InventoryUOM = dto.InventoryUOM,
                Mainsupplier = dto.Mainsupplier,
                ItemPreferredVendors = new[]
                {
                    new
                    {
                        BPCode = dto.BPCode
                        // CardType is optional—SL will ignore if null
                    }
                },
                U_PurchasingPrice = dto.U_PurchasingPrice,
                U_SalesPrice = dto.U_SalesPrice,
                ItemPrices = new[]
                {
                    new
                    {
                        PriceList     = 11,
                        Price         = dto.U_PurchasingPrice,
                        BasePriceList = 11,
                        Factor        = 1.0,
                        UoMPrices     = new object[0]
                    },
                    new
                    {
                        PriceList     = 12,
                        Price         = dto.U_SalesPrice,
                        BasePriceList = 12,
                        Factor        = 1.0,
                        UoMPrices     = new object[0]
                    }
                }
            };
            var json = JsonConvert.SerializeObject(
                payload,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }
            );
            Debug.WriteLine("📤 SL CreateItem header payload:");
            Debug.WriteLine(json);

            // Copy cookies (B1SESSION, ROUTEID) from SlClient
            var sl = _client as ServiceLayerClient;
            if (sl != null)
            {
                var baseUri = sl.HttpClient.BaseAddress;
                var auth = new Uri(baseUri.GetLeftPart(UriPartial.Authority));
                var cookies = sl.Cookies.GetCookies(auth);
                var sess = cookies["B1SESSION"]?.Value;
                var route = cookies["ROUTEID"]?.Value;
                if (!string.IsNullOrEmpty(sess))
                {
                    sl.HttpClient.DefaultRequestHeaders.Remove("Cookie");
                    sl.HttpClient.DefaultRequestHeaders.Add(
                        "Cookie",
                        $"B1SESSION={sess}; ROUTEID={route}"
                    );
                }
            }

            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            {
                var resp = await _client.HttpClient
                                         .PostAsync("Items", content)
                                         .ConfigureAwait(false);
                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                Debug.WriteLine($"📥 SL CreateItem response: {(int)resp.StatusCode} {resp.ReasonPhrase}");
                Debug.WriteLine(body);

                // If it’s 400 and the response message says “already exists”, let it throw
                if (resp.StatusCode == HttpStatusCode.BadRequest)
                {
                    // We’ll allow the caller to catch it and interpret “already exists”
                    throw new HttpRequestException($"400 Bad Request: {body}");
                }

                resp.EnsureSuccessStatusCode();
            }
        }

        /// <summary>
        /// Performs a GET /Items('<itemCode>') to retrieve the current U_PurchasingPrice & U_SalesPrice.
        /// </summary>
        public async Task<ItemDto> GetExistingItemAsync(string itemCode)
        {
            // Copy cookies over again:
            var sl = _client as ServiceLayerClient;
            if (sl != null)
            {
                var baseUri = sl.HttpClient.BaseAddress;
                var auth = new Uri(baseUri.GetLeftPart(UriPartial.Authority));
                var cookies = sl.Cookies.GetCookies(auth);
                var sess = cookies["B1SESSION"]?.Value;
                var route = cookies["ROUTEID"]?.Value;
                if (!string.IsNullOrEmpty(sess))
                {
                    sl.HttpClient.DefaultRequestHeaders.Remove("Cookie");
                    sl.HttpClient.DefaultRequestHeaders.Add(
                        "Cookie",
                        $"B1SESSION={sess}; ROUTEID={route}"
                    );
                }
            }

            // We only need to request the U_PurchasingPrice and U_SalesPrice fields:
            var url = $"Items('{Uri.EscapeDataString(itemCode)}')?$select=ItemCode,U_PurchasingPrice,U_SalesPrice";
            var resp = await _client.HttpClient.GetAsync(url).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                // If the item truly does not exist or other error, throw
                var errorBody = await resp.Content.ReadAsStringAsync();
                throw new HttpRequestException($"GET existing item failed ({(int)resp.StatusCode}): {errorBody}");
            }

            var rawJson = await resp.Content.ReadAsStringAsync();
            // The SL returns something like: { "ItemCode":"H-XYZ", "U_PurchasingPrice":50.00, "U_SalesPrice": 60.00 }
            var existing = JsonConvert.DeserializeObject<ItemDto>(rawJson);
            return existing;
        }

        /// <summary>
        /// PATCH /Items('<itemCode>') updating only U_PurchasingPrice and U_SalesPrice.
        /// </summary>
        public async Task PatchItemPricesAsync(string itemCode, double newPurchPrice, double newSalesPrice)
        {
            // Build a single payload with both the UDFs and the ItemPrices array:
            var patchPayload = new
            {
                U_PurchasingPrice = newPurchPrice,
                U_SalesPrice = newSalesPrice,

                ItemPrices = new[]
                {
            // Update the purchasing‐price row (PriceList = 11)
            new
            {
                PriceList = 11,
                Price     = newPurchPrice
            },
            // Update the sales‐price row (PriceList = 12)
            new
            {
                PriceList = 12,
                Price     = newSalesPrice
            }
        }
            };

            var json = JsonConvert.SerializeObject(
                patchPayload,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }
            );
            Debug.WriteLine("📤 SL PatchItemPrices payload:");
            Debug.WriteLine(json);

            // Copy cookies (B1SESSION, ROUTEID) as before
            var sl = _client as ServiceLayerClient;
            if (sl != null)
            {
                var baseUri = sl.HttpClient.BaseAddress;
                var auth = new Uri(baseUri.GetLeftPart(UriPartial.Authority));
                var cookies = sl.Cookies.GetCookies(auth);
                var sess = cookies["B1SESSION"]?.Value;
                var route = cookies["ROUTEID"]?.Value;
                if (!string.IsNullOrEmpty(sess))
                {
                    sl.HttpClient.DefaultRequestHeaders.Remove("Cookie");
                    sl.HttpClient.DefaultRequestHeaders.Add(
                        "Cookie",
                        $"B1SESSION={sess}; ROUTEID={route}"
                    );
                }
            }

            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            {
                var url = $"Items('{Uri.EscapeDataString(itemCode)}')";
                var resp = await _client.HttpClient.PatchAsync(url, content).ConfigureAwait(false);
                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                Debug.WriteLine($"📥 SL PatchItemPrices response: {(int)resp.StatusCode} {resp.ReasonPhrase}");
                Debug.WriteLine(body);
                resp.EnsureSuccessStatusCode();
            }
        }

    }
}
