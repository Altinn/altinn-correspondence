namespace Altinn.Correspondence.Application.ManualRetryNotPublishedCorrespondences;

public class ManualRetryNotPublishedCorrespondencesResponse
{
    public Dictionary<Guid, string> RetriedCorrespondenceIds { get; set; } = new Dictionary<Guid, string>();

}