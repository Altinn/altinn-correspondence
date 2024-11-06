namespace Altinn.Correspondence.Application.GetCorrespondenceHistory;

public class LegacyGetCorrespondenceHistoryRequest
{
    public required int OnBehalfOfPartyId { get; set; }
    public required Guid CorrespondenceId { get; set; }
}