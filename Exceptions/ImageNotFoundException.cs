using System;

namespace AvatarTemp.Exceptions
{
    public class ImageNotFoundException : Exception
    {
        public ImageNotFoundException(): base("Image not found.") { }
        public ImageNotFoundException(string message): base("Image not found: " + message) { }
        public ImageNotFoundException(string message, Exception innerException): base("Image not found: " + message, innerException) { }
    }
}
