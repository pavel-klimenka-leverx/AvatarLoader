
namespace AvatarTemp.Models
{
    public enum TechnicalLevel
    {
        D1, //Junior, Junior+
        D2, //Middle, Middle+
        D3, //Senior, Senior+
        D4  //Team Lead, Tech Lead
    }

    public class UserProfile
    {
        public string? Id { get; set; }
        public string? FullName { get; set; }
        
        public DateOnly Birthday { get; set; }
        public DateOnly EmploymentDate { get; set; }
        public int EnglishLevel { get; set; }
        public string? RoomNumber { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Position { get; set; }
        public string? ImageUrl { get; set; }
        public Guid? LesUserProfileId { get; set; }
        public Guid DepartmentId { get; set; }
        public Guid OfficeLocationId { get; set; }
        public Guid? UnitId { get; set; }
        public TechnicalLevel TechnicalLevel { get; set; }
    }
}
