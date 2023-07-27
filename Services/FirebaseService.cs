using Firebase.Storage;
using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using LeverX.Secrets;
using System.Net.Http.Json;
using System.Text;

namespace AvatarTemp.Services
{
    public class FirebaseService
    {
        public static void Initialize(string certificate)
        {
            certificate = certificate.Replace("{PLACE_SERVICE_KEY}", SecretManager.GetSecret("SERVICE_PRIVATE_KEY"));

            FirebaseApp.Create(new AppOptions()
            {
                Credential = GoogleCredential.FromJson(certificate)
            });
        }

        public async Task<UserRecord> UpdateUserImage(string userId, string imageUrl)
        {
            UserRecord user = await FirebaseAuth.DefaultInstance.GetUserAsync(userId);

            UserRecordArgs userArgs = new()
            {
                Uid = userId,
                Email = user.Email,
                EmailVerified = user.EmailVerified,
                Disabled = user.Disabled,
                DisplayName = user.DisplayName,
                PhotoUrl = imageUrl
            };

            return await FirebaseAuth.DefaultInstance.UpdateUserAsync(userArgs);
        }

        public async Task<UserRecord> GetUserFromFirebase(string userId)
        {
            return await FirebaseAuth.DefaultInstance.GetUserAsync(userId);
        }

        public async Task<UserRecord?> GetUserFromFirebaseByEmail(string email)
        {
            try
            {
                return await FirebaseAuth.DefaultInstance.GetUserByEmailAsync(email);
            }
            catch (FirebaseAuthException ex) when (ex.AuthErrorCode == AuthErrorCode.UserNotFound)
            {
                return null;
            }
        }

        public async Task<string> CreateEmptyUser(string email)
        {
            const string defaultImageUrl = "https://firebasestorage.googleapis.com/v0/b/lx-onb-recon-ui.appspot.com/o/6yvpkj.jpg?alt=media&token=965127ea-9e84-4dc4-9ea7-fa473f15434e";

            string name = email.Replace("@leverx.com", string.Empty).Split('.').Take(2)
                .Select(x => x.First().ToString().ToUpper() + x.Substring(1)).Aggregate((x, y) => x + " " + y);
            var user = new UserRecordArgs
            {
                Email = email,
                EmailVerified = true,
                Disabled = false,
                Password = Guid.NewGuid().ToString("N"),
                DisplayName = name,
                PhotoUrl = defaultImageUrl
            };
            var uid = (await FirebaseAuth.DefaultInstance.CreateUserAsync(user)).Uid;
            await FirebaseAuth.DefaultInstance.SetCustomUserClaimsAsync(uid,
                new Dictionary<string, object>() { { "role", new string[] { "User" } } });

            return uid;
        }

        public async Task<string> UploadUserAvatarToStorage(Stream file, string filename)
        {
            const string bucket = "lx-onb-recon-ui.appspot.com";

            string token = await TokenServer.GetToken();

            FirebaseStorage storage = new(bucket, new FirebaseStorageOptions
            {
                AuthTokenAsyncFactory = () => Task.FromResult(token),
                HttpClientTimeout = TimeSpan.FromSeconds(10),
                ThrowOnCancel = true
            });

            FirebaseStorageReference fileReference = storage.Child("images/avatars").Child(filename);
            await fileReference.PutAsync(file);
            return await fileReference.GetDownloadUrlAsync();
        }

        #region Helpers

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
