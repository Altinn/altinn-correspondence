using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Application.InitializeCorrespondence;

public class MakeAvailableInDialogportenRequest
{
    public Guid? CorrespondenceId { get; set; }
    public required bool CreateEvents { get; set; }
    public List<Guid>? CorrespondenceIds { get; set; }
}
