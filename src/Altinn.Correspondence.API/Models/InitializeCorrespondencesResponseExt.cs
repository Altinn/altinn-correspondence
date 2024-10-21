using Altinn.Correspondence.Application.InitializeCorrespondences;

namespace Altinn.Correspondence.API.Models;

public class InitializeCorrespondencesResponseExt
{
    public List<CorrespondenceDetails> Correspondences { get; set; }
    public List<Guid> AttachmentIds { get; set; }
}
