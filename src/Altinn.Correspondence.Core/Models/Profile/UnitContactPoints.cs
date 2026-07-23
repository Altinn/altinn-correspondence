namespace Altinn.Correspondence.Core.Models.Profile
{
    public class UnitContactPoints
    {
        public string OrganizationNumber { get; set; } = string.Empty;
        public List<UserRegisteredContactPoint> UserContactPoints { get; set; } = new List<UserRegisteredContactPoint>();
    }

    public class UserRegisteredContactPoint
    {
        public string? Email { get; set; }
        public string? MobileNumber { get; set; }
    }
}
