using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Application.InitializeCorrespondence;

public class MigrateCorrespondenceRequest
{
    public required int Altinn2CorrespondenceId { get; set; }
    public required CorrespondenceEntity CorrespondenceEntity { get; set; }
    public List<Guid> ExistingAttachments { get; set; }
}
