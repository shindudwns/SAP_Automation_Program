// File: Services/ServiceLayer/UnitOfMeasureService.cs
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SimplifyQuoter.Services.ServiceLayer.Dtos;

namespace SimplifyQuoter.Services.ServiceLayer
{
    public class UnitOfMeasureService
    {
        private readonly ServiceLayerClient _slClient;
        private HttpClient _http => _slClient.HttpClient;

        public UnitOfMeasureService(ServiceLayerClient slClient)
        {
            _slClient = slClient;
        }

        public async Task<int> GetEntryByCodeAsync(string code)
        {
            // re-apply cookies (exactly as you do in ItemService)
            var baseUri = _slClient.HttpClient.BaseAddress;
            var auth = new Uri(baseUri.GetLeftPart(UriPartial.Authority));
            var cookies = _slClient.Cookies.GetCookies(auth);
            var session = cookies["B1SESSION"]?.Value;
            var route = cookies["ROUTEID"]?.Value;
            if (!string.IsNullOrEmpty(session))
            {
                _http.DefaultRequestHeaders.Remove("Cookie");
                _http.DefaultRequestHeaders.Add("Cookie",
                    $"B1SESSION={session}; ROUTEID={route}");
            }

            // fetch the numeric entry for the given code
            var resp = await _http
                .GetAsync($"UnitOfMeasurements?$filter=UoMCode eq '{code}'&$select=UoMEntry")
                .ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            dynamic j = JsonConvert.DeserializeObject(await resp.Content.ReadAsStringAsync());
            if (j.value.Count == 0)
                throw new InvalidOperationException($"UoM '{code}' not found");
            return (int)j.value[0].UoMEntry;
        }
    }


}

// helper for OData array
public class ODataResponse<T>
{
    [JsonProperty("value")]
    public T[] Value { get; set; }
}
