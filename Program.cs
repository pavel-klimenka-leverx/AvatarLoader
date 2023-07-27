using AvatarTemp.Exceptions;
using AvatarTemp.Models;
using AvatarTemp.Services;
using LeverX.Secrets;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;

namespace AvatarTemp
{
    internal class Program
    {
        #region Helper classes
        private class UserImage
        {
            public UserProfile User { get; set; } = null!;
            public MemoryStream Image { get; set; } = null!;

            public UserImage(UserProfile user, MemoryStream image) 
            {
                User = user;
                Image = image;
            }
        }
        #endregion

        private static IConfiguration _configuration;
        private static Logger _logger;
        private static LesService _lesService;
        private static BaseDataService _baseDataService;
        private static FirebaseService _firebaseService;
        private static ApplicationParameters _parameters;

        static Program()
        {
            Log("\nInitializing, please wait...");

            SecretManager.Initialize();
            FirebaseService.Initialize(File.ReadAllText("service.json"));

            _logger = new();
            _configuration = new ConfigurationBuilder().AddEnvironmentVariables().AddJsonFile("appsettings.json").Build();
            _parameters = _configuration.GetSection("ApplicationParameters").Get<ApplicationParameters>() ?? throw LogException("Failed to get application parameters from json file.");
            _lesService = new(_logger);
            _baseDataService = new(_configuration, _logger);
            _firebaseService = new();
        }

        static async Task Main(string[] args)
        {
            if (_parameters.DryRun) Console.WriteLine("\nThe program will run in 'Dry Run' mode. Press any key to continue...");
            else Console.WriteLine("\nThe program will run in 'For Real' mode. Press any key to continue...");
            Console.ReadKey(true);

            while(await MainMenu());
        }

        private static async Task<bool> MainMenu()
        {
            Log();
            Log("1) Update images,");
            Log("2) Validate emails");
            Log("Q) Exit");
            ConsoleKey input = Console.ReadKey().Key;

            switch(input)
            {
                case ConsoleKey.D1:
                    await UpdateImagesCmd();
                    return true;
                case ConsoleKey.D2:
                    await ValidateEmailsCmd();
                    return true;
                case ConsoleKey.Q:
                default:
                    return false;
            }
        }

        private static async Task ValidateEmailsCmd()
        {
            Log("\n<> Fetching all users from BaseUserApi...");
            List<UserProfile> users = await _baseDataService.GetAllUsers();
            Log($"\n<> Fetched {users.Count} users.");

            Regex emailRegex = new Regex(@"^[a-z]+\.[a-z]+@leverx.com$", RegexOptions.Compiled);
            uint problemCount = 0;

            foreach(UserProfile user in users)
            {
                if (user.Email == null)
                {
                    Log($"--> PROBLEM: User withour email field, Id: {user.Id ?? ""}");
                    ++problemCount;
                }
                else
                {
                    if (emailRegex.IsMatch(user.Email))
                    {
                        Log($"Passed: {user.Email}");
                    }
                    else
                    {
                        Log($"--> PROBLEM: {user.Email}");
                        ++problemCount;
                    }
                }
            }

            Log($"\nFound {problemCount} problems.");
        }

        private static async Task UpdateImagesCmd()
        {
            Log("\n<> Fetching all users from BaseUserApi...");
            List<UserProfile> users = await _baseDataService.GetAllUsers();
            Log($"\n<> Fetched {users.Count} users.");

            List<UserImage> images = new();

            try
            {
                Log("\n<> Fetching images from LES...");

                bool skipDownloadErrors = false;
                ulong memoryUsed = 0u;
                foreach(UserProfile user in users)
                {
                    if(user.Email == null)
                    {
                        Log("User without email field, continuing...");
                        continue;
                    }

                    MemoryStream imageStream;
                    try
                    {
                        imageStream = _parameters.DryRun ? new MemoryStream() : await _lesService.GetUserPicture(user.Email);
                        memoryUsed += (ulong)imageStream.Length;
                        images.Add(new UserImage(user, imageStream));
                        Log($"Fetched: {user.Email}; memory usage = {memoryUsed}");
                        Thread.Sleep((int)(_parameters.LesFetchDelaySec * 1000));
                    }
                    catch(ImageNotFoundException ex)
                    {
                        Log(ex.Message);

                        if(skipDownloadErrors)
                        {}
                        else
                        {
                            Log("Skip? y/n/a(all)");
                            ConsoleKey input = Console.ReadKey().Key;
                            switch(input)
                            {
                                case ConsoleKey.A:
                                    skipDownloadErrors = true;
                                    break;
                                case ConsoleKey.Y:
                                    break;
                                case ConsoleKey.N:
                                default:
                                    Log("\n <> Exiting...");
                                    goto END;
                            }
                        }

                    }
                }

                Log("\n<> Uploading images to firebase storage...");
                foreach(UserImage image in images)
                {
                    image.User.ImageUrl = _parameters.DryRun ? "https://DRY_RUN.com" : await _firebaseService.UploadUserAvatarToStorage(image.Image, _parameters.AvatarFilenameTemplate.Replace("{{email}}", image.User.Email));

                    Log($"{image.User.Email!} >> {image.User.ImageUrl};");
                }

                Log("\n<> Updating database records for image urls...");
                foreach(UserImage image in images)
                {
                    if (_parameters.DryRun) ;
                    else await _baseDataService.UpdateUser(image.User);

                    Log(image.User.Email!);
                }

                Log("\n<> Updating firebase 'PhotoUrl' claims...");
                foreach(UserImage image in images)
                {
                    if (_parameters.DryRun) ;
                    else await _firebaseService.UpdateUserImage(image.User.Id!, image.User.ImageUrl!);

                    Log(image.User.Email!);
                }


                END:
                Log("\n<> Done.");
            }
            finally
            {
                foreach (UserImage image in images)
                {
                    image.Image.Close();
                }
            }
        }

        private static void Log(string message = "")
        {
            Console.WriteLine(message);
        }

        private static Exception LogException(string message)
        {
            Log(message);
            return new Exception(message);
        }
    }
};