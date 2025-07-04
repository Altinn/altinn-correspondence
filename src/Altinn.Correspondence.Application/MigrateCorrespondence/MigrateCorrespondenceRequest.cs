using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Application.MigrateCorrespondence;

public class MigrateCorrespondenceRequest
{
    public required int Altinn2CorrespondenceId { get; set; }
    public required CorrespondenceEntity CorrespondenceEntity { get; set; }
    public List<Guid> ExistingAttachments { get; set; }
    public bool MakeAvailable { get; set; } = false;
}
