
namespace AvatarTemp
{
    public class Logger
    {
        public void LogInfo(string message)
        {
            Print("[INFO] " + message);
        }

        public void LogWarning(string message)
        {
            Print("[WARNING] " + message);
        }

        public void LogError(string message)
        {
            Print("[ERROR] " + message);
        }

        private void Print(string message)
        {
            Console.WriteLine($"{DateTime.Now} {message}");
        }
    }
}
