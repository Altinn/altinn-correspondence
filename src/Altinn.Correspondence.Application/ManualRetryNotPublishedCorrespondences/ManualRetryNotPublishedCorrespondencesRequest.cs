namespace Altinn.Correspondence.Application.ManualRetryNotPublishedCorrespondences;
public class ManualRetryNotPublishedCorrespondencesRequest
{
    public List<Guid> CorrespondenceIds { get; set; } = new List<Guid>();
}