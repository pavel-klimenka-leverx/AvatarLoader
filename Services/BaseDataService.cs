using AvatarTemp.Models;
using LeverX.Secrets;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace AvatarTemp.Services
{
    public class BaseDataService
    {
        private readonly string _baseDataUrl;
        private readonly Logger _logger;

        public BaseDataService(IConfiguration configuration, Logger logger)
        {
            _logger = logger;
            _baseDataUrl = configuration.GetConnectionString("BaseData") ?? throw LogException("Failed to get BaseData url.");
        }

        public async Task<List<UserProfile>> GetAllUsers()
        {
            const string url = "user-profiles";

            using HttpClient client = await CreateAuthorizedHttpClient(_baseDataUrl);

            List<UserProfile> users = await client.GetFromJsonAsync<List<UserProfile>>(url) ?? throw LogException("Failed to get base data users.");

            return users;
        }

        public async Task UpdateUser(UserProfile user)
        {
            const string url = "user-profiles";

            using HttpClient client = await CreateAuthorizedHttpClient(_baseDataUrl);

            (await client.PutAsJsonAsync<UserProfile>(url, user)).EnsureSuccessStatusCode();
        }

        #region Helpers

        private Exception LogException(string message)
        {
            _logger.LogError(message);
            return new Exception(message);
        }

        private async Task<HttpClient> CreateAuthorizedHttpClient(string? baseUrl = null)
        {
            string token = await TokenServer.GetToken();

            HttpClient client = new();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (baseUrl != null) client.BaseAddress = new Uri(baseUrl);

            return client;
        }

        private static class TokenServer
        {
            #region Helper classes

            private class LoginResult
            {
                public string? Message { get; set; }
                public string? Email { get; set; }
                public string? IdToken { get; set; }
                public bool Success { get; set; } = true;
            }

            #endregion

            private const int TokenExpirationTimeMin = 50;

            private static string? _token;
            private static DateTime _tokenExpirationDate;
            private static SemaphoreSlim _tokenSemaphore = new(1);

            public static async Task<string> GetToken()
            {
                try
                {
                    _tokenSemaphore.Wait();
                    await TryUpdateToken();
                }
                finally
                {
                    _tokenSemaphore.Release();
                }

                return _token ?? throw new Exception("Failed to fetch firebase token: token was null");
            }

            private static async Task TryUpdateToken()
            {
                if (_token == null || DateTime.Now > _tokenExpirationDate)
                {
                    string[] credentials = SecretManager.GetSecret("PEER_JOB_CREDENTIALS")?.Split('#') ??
                        throw new Exception("Failed to get firebase credentials string.");

                    if (credentials.Length != 2) throw new Exception("Invalid firebase credential string.");

                    string webApiKey = SecretManager.GetSecret("WEB_API_KEY") ?? throw new Exception("Failed to get firebase web_api_key");

                    using HttpClient client = new();
                    HttpRequestMessage msg = new HttpRequestMessage(HttpMethod.Post, $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={webApiKey}");
                    msg.Content = new StringContent($"{{\"email\":\"{credentials[0]}\",\"password\":\"{credentials[1]}\"" +
                                                    $",\"returnSecureToken\":true}}", Encoding.UTF8, "application/json");

                    HttpResponseMessage responseMessage = await client.SendAsync(msg);
                    responseMessage.EnsureSuccessStatusCode();

                    _token = (await responseMessage.Content.ReadFromJsonAsync<LoginResult>())?.IdToken ??
                        throw new Exception("Failed to get firebase token: response parsing resulted in a null value");
                    _tokenExpirationDate = DateTime.Now + TimeSpan.FromMinutes(TokenExpirationTimeMin);
                }
            }
        }

        #endregion
    }
}
