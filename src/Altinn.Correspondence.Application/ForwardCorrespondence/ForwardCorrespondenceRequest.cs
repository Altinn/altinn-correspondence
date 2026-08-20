namespace Altinn.Correspondence.Application.ForwardCorrespondence;

public class ForwardCorrespondenceRequest
{
    public Guid CorrespondenceId { get; set; }
    public required string ForwardTo { get; set; }
    public string? ForwardingText { get; set; }
}