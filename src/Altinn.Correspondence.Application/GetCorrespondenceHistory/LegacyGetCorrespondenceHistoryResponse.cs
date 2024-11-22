using Altinn.Correspondence.Core.Models.Notifications;

namespace Altinn.Correspondence.Application.GetCorrespondenceHistory;

public class LegacyGetCorrespondenceHistoryResponse
{
    public string Status { get; set; }
    public DateTimeOffset? StatusChanged { get; set; }
    public string StatusText { get; set; }
    public LegacyUser User { get; set; }
}
public class LegacyUser
{
    public int PartyId { get; set; }
    public int AuthenticationLevel { get; set; }
    public Recipient Recipient { get; set; }
}