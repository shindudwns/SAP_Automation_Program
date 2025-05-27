// File: Services/ServiceLayer/ServiceLayerClient.cs
using System;
using System.Configuration;
using System.Diagnostics;
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

            // Allow any SSL cert (for self-signed)—remove this in production!
            var handler = new HttpClientHandler
            {
                CookieContainer = _cookies,
                UseCookies = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
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
            var payload = new
            {
                CompanyDB = companyDb,
                UserName = user,
                Password = pass
            };
            var json = JsonConvert.SerializeObject(payload);

            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            {
                HttpResponseMessage resp;
                try
                {
                    resp = await _http.PostAsync("Login", content);
                }
                catch (Exception ex)
                {
                    // Network-level failure
                    Debug.WriteLine($"[SL Login] HTTP error: {ex}");
                    throw new InvalidOperationException(
                        $"SL Login HTTP error: {ex.Message}", ex);
                }

                // Always capture and log the response body
                var body = await resp.Content.ReadAsStringAsync();

                Debug.WriteLine("=== SL Login Response ===");
                Debug.WriteLine($"URL:    {_http.BaseAddress}Login");
                Debug.WriteLine($"Status: {(int)resp.StatusCode} {resp.ReasonPhrase}");
                Debug.WriteLine("Body:");
                Debug.WriteLine(body);
                Debug.WriteLine("=========================");

                if (!resp.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        $"SL Login failed {(int)resp.StatusCode} {resp.ReasonPhrase}");
                }

                IsLoggedIn = true;
            }
        }

        public async Task LogoutAsync()
        {
            if (!IsLoggedIn) return;

            HttpResponseMessage resp;
            try
            {
                resp = await _http.PostAsync("Logout", null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SL Logout] HTTP error: {ex}");
                throw new InvalidOperationException(
                    $"SL Logout HTTP error: {ex.Message}", ex);
            }

            // Log response for debugging
            var body = await resp.Content.ReadAsStringAsync();
            Debug.WriteLine("=== SL Logout Response ===");
            Debug.WriteLine($"URL:    {_http.BaseAddress}Logout");
            Debug.WriteLine($"Status: {(int)resp.StatusCode} {resp.ReasonPhrase}");
            Debug.WriteLine("Body:");
            Debug.WriteLine(body);
            Debug.WriteLine("==========================");

            resp.EnsureSuccessStatusCode();
            IsLoggedIn = false;
            _cookies = new CookieContainer(); // clear session
        }
    }
}
