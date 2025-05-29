// File: Services/ServiceLayer/ServiceLayerClient.cs
using System;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SimplifyQuoter.Services.ServiceLayer
{
    // DTO to deserialize the login JSON
    class LoginResponseDto
    {
        [JsonProperty("SessionId")]
        public string SessionId { get; set; }
    }

    public class ServiceLayerClient : IServiceLayerClient
    {
        private CookieContainer _cookies = new CookieContainer();
        private readonly HttpClient _http;

        public bool IsLoggedIn { get; private set; }
        public HttpClient HttpClient => _http;
        public CookieContainer Cookies => _cookies;

        public ServiceLayerClient()
        {
            var baseUrl = ConfigurationManager.AppSettings["ServiceLayer:Url"]
                          ?? throw new InvalidOperationException(
                              "Configure <add key=\"ServiceLayer:Url\" /> in App.config");

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

            // Always disable Expect-Continue
            _http.DefaultRequestHeaders.ExpectContinue = false;
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

            HttpResponseMessage resp;
            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            {
                try
                {
                    resp = await _http.PostAsync("Login", content);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SL Login] HTTP error: {ex}");
                    throw new InvalidOperationException(
                        $"SL Login HTTP error: {ex.Message}", ex);
                }
            }

            var body = await resp.Content.ReadAsStringAsync();
            Debug.WriteLine("=== SL Login Response ===");
            Debug.WriteLine($"URL:    {_http.BaseAddress}Login");
            Debug.WriteLine($"Status: {(int)resp.StatusCode} {resp.ReasonPhrase}");
            Debug.WriteLine("Body:");
            Debug.WriteLine(body);

            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"SL Login failed {(int)resp.StatusCode} {resp.ReasonPhrase}");

            // 1) Parse the JSON to get the SessionId
            var loginDto = JsonConvert.DeserializeObject<LoginResponseDto>(body);

            // 2) Manually add the B1SESSION cookie
            var authorityUri = new Uri(_http.BaseAddress.GetLeftPart(UriPartial.Authority));
            _cookies.Add(authorityUri,
                         new Cookie("B1SESSION", loginDto.SessionId));

            Debug.WriteLine($"[SL Login] Manually added B1SESSION={loginDto.SessionId}");

            // 3) Optionally log whatever ROUTEID the container did already pick up
            var routeCookie = _cookies.GetCookies(authorityUri)["ROUTEID"]?.Value;
            Debug.WriteLine($"[SL Login] ROUTEID from container: {routeCookie}");

            // 4) (Optional) stomp it into the header so you’re 100% sure
            _http.DefaultRequestHeaders.Remove("Cookie");
            _http.DefaultRequestHeaders.Add("Cookie",
                $"B1SESSION={loginDto.SessionId}; ROUTEID={routeCookie}");

            Debug.WriteLine($"[SL Login] Final Cookie header: " +
                            _http.DefaultRequestHeaders.GetValues("Cookie").Single());

            IsLoggedIn = true;
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

            // Clear out everything
            resp.EnsureSuccessStatusCode();
            IsLoggedIn = false;
            _cookies = new CookieContainer();
        }
    }
}
