namespace Altinn.Correspondence.Application.GetCorrespondenceDetails;

public class GetCorrespondenceDetailsRequest
{
    public required Guid CorrespondenceId { get; set; }
    public string? OnBehalfOf { get; set; }
}