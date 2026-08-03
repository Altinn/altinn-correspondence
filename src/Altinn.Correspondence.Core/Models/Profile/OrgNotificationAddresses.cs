namespace Altinn.Correspondence.Core.Models.Profile
{
    public class OrgNotificationAddresses
    {
        public string OrganizationNumber { get; set; } = string.Empty;
        public List<string> EmailList { get; set; } = new List<string>();
        public List<string> MobileNumberList { get; set; } = new List<string>();
    }
}
