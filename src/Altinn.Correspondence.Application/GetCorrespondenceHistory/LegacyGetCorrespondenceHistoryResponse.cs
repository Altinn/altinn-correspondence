namespace Altinn.Correspondence.Application.GetCorrespondenceHistory;

public class LegacyGetCorrespondenceHistoryResponse
{
    public List<LegacyCorrespondenceStatus> History { get; set; } = new List<LegacyCorrespondenceStatus>();

    public bool? NeedsConfirm { get; set; }
}
public class LegacyCorrespondenceStatus
{
    public string Status { get; set; }
    public DateTimeOffset? StatusChanged { get; set; }
    public string StatusText { get; set; }
    
    public LegacyUser User { get; set; }
}
public class LegacyUser
{
    public string PartyId { get; set; }
    public int AuthenticationLevel { get; set; }
}