namespace Altinn.Correspondence.API.Models;

public class InitializeCorrespondencesResponseExt
{
    public List<Guid> CorrespondenceIds { get; set; }
    public List<Guid> AttachmentIds { get; set; }
}
