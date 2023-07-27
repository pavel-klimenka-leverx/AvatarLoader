using AvatarTemp.Exceptions;
using LeverX.Secrets;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AvatarTemp.Services
{
    public class LesService
    {
        #region Helper classes

        private class LesPictureDto
        {
            public class LesUserDto
            {
                [JsonPropertyName("first_name")]
                public string? FirstName { get; set; }
                [JsonPropertyName("last_name")]
                public string? LastName { get; set; }
                public string? Email { get; set; }
                [JsonPropertyName("user_avatar")]
                public string? UserAvatarUrl { get; set; }
            }
            public LesUserDto? User { get; set; }
        }

        #endregion

        private Logger _logger;

        public LesService(Logger logger)
        {
            _logger = logger;
        }

        public async Task<MemoryStream> GetUserPicture(string userEmail)
        {
            const string lesPictureUrl = "https://les.leverx-group.com/api/employees/employee-avatar/";

            using HttpClient client = await CreateAuthorizedHttpClient(lesPictureUrl);
            HttpResponseMessage response = await client.GetAsync(userEmail);

            if(!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                    throw new ImageNotFoundException(userEmail);
                else
                    throw LogException($"Failed to fetch image from LES. Status code: {response.StatusCode}; Message: {await response.Content.ReadAsStringAsync()}");
            }

            LesPictureDto? pictureDto = await response.Content.ReadFromJsonAsync<LesPictureDto>();
            if (pictureDto?.User?.UserAvatarUrl == null) throw LogException("Failed to get LES user avatar: parsing response from LES resulted in a null value.");

            using HttpClient downloadClient = new();
            using Stream responseData = await downloadClient.GetStreamAsync(pictureDto.User.UserAvatarUrl);
            MemoryStream pictureData = new();
            await responseData.CopyToAsync(pictureData);
            pictureData.Position = 0;
            return pictureData;
        }

        #region Helpers

        private async Task<HttpClient> CreateAuthorizedHttpClient(string? baseUrl = null)
        {
            string token;
            try
            {
                token = await TokenServer.GetToken();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to fetch les token: {ex.Message}");
                throw;
            }

            HttpClient client = new();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (baseUrl != null) client.BaseAddress = new Uri(baseUrl);
            return client;
        }

        private Exception LogException(string message)
        {
            _logger.LogError(message);
            return new Exception(message);
        }

        private static class TokenServer
        {
            #region HelperClasses
            private class LesTokenDto
            {
                public string? Token { get; set; }

                [JsonPropertyName("expires_in")]
                public long ExpiresIn { get; set; }
            }
            #endregion

            private const string TokenUrl = "https://les.leverx-group.com/api/auth/login-email";

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

                return _token ?? throw new Exception("Failed to get les token: token was equal to null.");
            }

            private static async Task TryUpdateToken()
            {
                if (string.IsNullOrEmpty(_token) || DateTime.Now > _tokenExpirationDate)
                {
                    string[] credentials = SecretManager.GetSecret("les-integration-user").Split('#');
                    if (credentials.Length != 2) throw new Exception("Invalid les user credentials: credential string didn't split into 2 parts.");

                    using HttpClient client = new();
                    HttpResponseMessage response = await client.PostAsJsonAsync(TokenUrl, new { Email = credentials[0], Password = credentials[1] });
                    response.EnsureSuccessStatusCode();
                    LesTokenDto? tokenDto = await response.Content.ReadFromJsonAsync<LesTokenDto>();
                    if (tokenDto?.Token == null) throw new Exception("Failed to fetch les token: parsing json response from LES resulted in a null value.");

                    _token = tokenDto.Token;
                    _tokenExpirationDate = DateTime.Now + TimeSpan.FromMilliseconds(tokenDto.ExpiresIn);
                }
            }
        }

        #endregion
    }

}
