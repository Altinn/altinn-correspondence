using Altinn.Correspondence.Core.Models;

namespace Altinn.Correspondence.Application.InitializeCorrespondenceCommand;

public class InitializeCorrespondenceCommandRequest
{
    public required CorrespondenceEntity Correspondence { get; set; }
}
