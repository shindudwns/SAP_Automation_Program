// File: Services/ServiceLayer/HttpClientExtensions.cs
using System.Net.Http;
using System.Threading.Tasks;

namespace SimplifyQuoter.Services.ServiceLayer
{
    public static class HttpClientExtensions
    {
        /// <summary>
        /// Allows you to call `httpClient.PatchAsync(...)` just like PostAsync or PutAsync.
        /// </summary>
        public static Task<HttpResponseMessage> PatchAsync(this HttpClient client, string requestUri, HttpContent content)
        {
            // Create an HttpRequestMessage with method "PATCH"
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), requestUri)
            {
                Content = content
            };
            return client.SendAsync(request);
        }
    }
}
