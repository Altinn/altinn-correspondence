using Altinn.Correspondence.Core.Models;

namespace Altinn.Correspondence.Application.InitializeCorrespondenceCommand;

public class InitializeCorrespondenceCommandRequest
{
    public CorrespondenceEntity correspondence { get; set; }
}
