
namespace AvatarTemp.Models
{
    public class UserProfile
    {
        public string? Id { get; set; }
        public string? FullName { get; set; }
        
        public DateOnly Birthday { get; set; }
        public DateOnly EmploymentDate { get; set; }
        public int EnglishLevel { get; set; }
        public Guid OfficeLocationId { get; set; }
        public string? RoomNumber { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Position { get; set; }
        public string? ImageUrl { get; set; }
    }
}
