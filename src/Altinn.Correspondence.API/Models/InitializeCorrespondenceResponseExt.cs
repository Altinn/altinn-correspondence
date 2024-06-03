namespace Altinn.Correspondence.API.Models;

public class InitializeCorrespondenceResponseExt
{
    public Guid CorrespondenceId { get; set; }

    public List<Guid> AttachmentIds { get; set; } = new List<Guid>();
}
