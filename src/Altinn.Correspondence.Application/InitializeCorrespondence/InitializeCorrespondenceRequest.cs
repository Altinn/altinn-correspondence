using Altinn.Correspondence.Core.Models;

namespace Altinn.Correspondence.Application.InitializeCorrespondence;

public class InitializeCorrespondenceRequest
{
    public required CorrespondenceEntity Correspondence { get; set; }
}
