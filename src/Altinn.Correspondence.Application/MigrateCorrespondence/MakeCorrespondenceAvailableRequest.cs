using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Application.MigrateCorrespondence;

public class MakeCorrespondenceAvailableRequest
{
    public Guid? CorrespondenceId { get; set; }
    public bool CreateEvents { get; set; }
    public List<Guid>? CorrespondenceIds { get; set; }
}
