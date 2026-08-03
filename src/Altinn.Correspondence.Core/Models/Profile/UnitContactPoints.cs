namespace Altinn.Correspondence.Core.Models.Profile
{
    public class UnitContactPoints
    {
        public string OrganizationNumber { get; set; } = string.Empty;
        public int PartyId { get; set; }
        public List<UserRegisteredContactPoint> UserContactPoints { get; set; } = new List<UserRegisteredContactPoint>();
    }

    public class UserRegisteredContactPoint
    {
        public int UserId { get; set; }
        public string? Email { get; set; }
        public string? MobileNumber { get; set; }
    }
}
