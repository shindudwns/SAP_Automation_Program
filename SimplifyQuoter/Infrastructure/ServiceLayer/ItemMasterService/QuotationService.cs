// File: Services/ServiceLayer/QuotationService.cs
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Text;
using System.Net.Http;

namespace SimplifyQuoter.Services.ServiceLayer
{
    /// <summary>
    /// POST /b1s/v1/SalesQuotations per metadata.
    /// </summary>
    public class QuotationService
    {
        private readonly IServiceLayerClient _client;
        public QuotationService(IServiceLayerClient client) => _client = client;

        public async Task CreateAsync(Dtos.QuotationDto dto)
        {
            var json = JsonConvert.SerializeObject(dto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _client.HttpClient.PostAsync("SalesQuotations", content);
            resp.EnsureSuccessStatusCode(); // 201 Created
        }
    }
}
