// File: Services/ServiceLayer/ServiceLayerClient.cs
using System;
using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SimplifyQuoter.Services.ServiceLayer
{
    /// <summary>
    /// Implements Login/Logout per SAP SL docs:
    /// POST /b1s/v1/Login → gets B1SESSION & ROUTEID;
    /// POST /b1s/v1/Logout → ends session.
    /// </summary>
    public class ServiceLayerClient : IServiceLayerClient
    {
        private CookieContainer _cookies = new CookieContainer();
        private readonly HttpClient _http;

        public bool IsLoggedIn { get; private set; }
        public HttpClient HttpClient => _http;

        public ServiceLayerClient()
        {
            var baseUrl = ConfigurationManager.AppSettings["ServiceLayer:Url"]
                          ?? throw new InvalidOperationException(
                              "Configure <add key=\"ServiceLayer:Url\" /> in App.config");
            var handler = new HttpClientHandler
            {
                CookieContainer = _cookies,
                UseCookies = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            _http = new HttpClient(handler)
            {
                BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/b1s/v1/")
            };
            _http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task LoginAsync(string companyDb, string user, string pass)
        {
            var payload = new { CompanyDB = companyDb, UserName = user, Password = pass };
            var json = JsonConvert.SerializeObject(payload);
            var resp = await _http.PostAsync(
                "Login",
                new StringContent(json, Encoding.UTF8, "application/json")
            );
            resp.EnsureSuccessStatusCode();  // 200 OK + Set-Cookie: B1SESSION + ROUTEID :contentReference[oaicite:0]{index=0}
            IsLoggedIn = true;
        }

        public async Task LogoutAsync()
        {
            if (!IsLoggedIn) return;
            var resp = await _http.PostAsync("Logout", null);
            resp.EnsureSuccessStatusCode();  // 204 No Content
            IsLoggedIn = false;
            _cookies = new CookieContainer(); // clear session
        }
    }
}
