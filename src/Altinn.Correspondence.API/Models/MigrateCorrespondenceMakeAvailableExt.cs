using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.API.Models;

public class MigrateCorrespondenceMakeAvailableExt
{
    public Guid? CorrespondenceId { get; set; }
    public required bool CreateEvents { get; set; } = false;
    public List<Guid>? CorrespondenceIds { get; set; }
}
