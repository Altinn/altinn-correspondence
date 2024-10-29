namespace Altinn.Correspondence.Application.GetCorrespondenceOverview;

public class LegacyGetCorrespondenceOverviewRequest
{
    public required Guid CorrespondenceId { get; set; }

    public required int PartyId { get; set; }

}
