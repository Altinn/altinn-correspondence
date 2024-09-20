
namespace Altinn.Correspondence.Core.Models.Notifications;

public class Recipient
{
    public string? EmailAddress { get; set; }

    public string? MobileNumber { get; set; }

    public string? OrganizationNumber { get; set; }

    public string? NationalIdentityNumber { get; set; }

    public bool? IsReserved { get; set; }
}