// File: Services/ServiceLayer/IServiceLayerClient.cs
using System.Net.Http;
using System.Threading.Tasks;

namespace SimplifyQuoter.Services.ServiceLayer
{
    /// <summary>
    /// Manages login/logout and exposes a HttpClient
    /// whose CookieContainer holds B1SESSION & ROUTEID.
    /// </summary>
    public interface IServiceLayerClient
    {
        bool IsLoggedIn { get; }
        HttpClient HttpClient { get; }
        Task LoginAsync(string companyDb, string user, string pass);
        Task LogoutAsync();
    }
}
