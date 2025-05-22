// File: Services/ServiceLayer/ItemService.cs
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Text;
using System.Net.Http;

namespace SimplifyQuoter.Services.ServiceLayer
{
    /// <summary>
    /// CRUD for /b1s/v1/Items per metadata (Item Master Data).
    /// </summary>
    public class ItemService
    {
        private readonly IServiceLayerClient _client;
        public ItemService(IServiceLayerClient client) => _client = client;

        public async Task CreateOrUpdateAsync(Dtos.ItemDto dto)
        {
            var json = JsonConvert.SerializeObject(dto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _client.HttpClient.PostAsync("Items", content);
            resp.EnsureSuccessStatusCode(); // 201 Created or 204 No Content if Prefer:return-no-content
        }
    }
}
