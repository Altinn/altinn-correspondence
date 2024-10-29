namespace Altinn.Correspondence.Application.GetCorrespondenceHistory;

public class LegacyGetCorrespondenceHistoryRequest
{
    public required string OnBehalfOfPartyId { get; set; }
    public required Guid CorrespondenceId { get; set; }
}