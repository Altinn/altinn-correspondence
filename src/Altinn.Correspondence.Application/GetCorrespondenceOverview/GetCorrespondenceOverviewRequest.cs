namespace Altinn.Correspondence.Application.GetCorrespondenceOverview;

public class GetCorrespondenceOverviewRequest
{
    public Guid CorrespondenceId { get; set; }
    public string? OnBehalfOf { get; set; }
}