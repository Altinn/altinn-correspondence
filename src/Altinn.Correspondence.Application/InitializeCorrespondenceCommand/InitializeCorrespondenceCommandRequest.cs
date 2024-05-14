using Altinn.Correspondence.Core.Models;

namespace Altinn.Correspondence.Application.InitializeCorrespondenceCommand;

public class InitializeCorrespondenceCommandRequest
{
    public CorrespondenceEntity Correspondence { get; set; }
}
