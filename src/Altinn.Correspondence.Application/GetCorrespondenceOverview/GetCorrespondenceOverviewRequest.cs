namespace Altinn.Correspondence.Application.GetCorrespondenceOverview;

public class GetCorrespondenceOverviewRequest
{
    public required Guid CorrespondenceId { get; set; }
    public string? OnBehalfOf { get; set; }
}