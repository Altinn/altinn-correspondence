namespace Altinn.Correspondence.Application.PurgeCorrespondence;

public class PurgeCorrespondenceRequest
{
    public required Guid CorrespondenceId { get; set; }
    public string? OnBehalfOf { get; set; }
}