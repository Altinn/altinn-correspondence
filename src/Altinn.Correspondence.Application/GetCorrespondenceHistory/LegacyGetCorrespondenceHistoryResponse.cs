using Altinn.Correspondence.Core.Models.Notifications;

namespace Altinn.Correspondence.Application.GetCorrespondenceHistory;

public class LegacyGetCorrespondenceHistoryResponse
{
    public string Status { get; set; }
    public DateTimeOffset? StatusChanged { get; set; }
    public string StatusText { get; set; }
    public LegacyUser User { get; set; }
    public LegacyNotification? Notification { get; set; }
}
public class LegacyUser
{
    public int? PartyId { get; set; }
    public string? NationalIdentityNumber { get; set; }
    public string? Name { get; set; }
}
public class LegacyNotification
{
    public string? EmailAddress { get; set; }
    public string? MobileNumber { get; set; }
    public string? OrganizationNumber { get; set; }
    public string? NationalIdentityNumber { get; set; }
}