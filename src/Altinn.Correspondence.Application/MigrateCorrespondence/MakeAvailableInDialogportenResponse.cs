using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Application.InitializeCorrespondence;

public class MakeAvailableInDialogportenResponse
{
    public Guid? CorrespondenceId { get; set; }
    public required bool IsAlreadyMadeAvailable { get; set; }

    public List<Guid>? CorrespondenceIds { get; set; }
}
