using Altinn.Correspondence.Core.Models.Enums;
namespace Altinn.Correspondence.Application.ForwardCorrespondence;

public class CanCorrespondenceBeForwardedResponse
{
    public required bool AllowForwarding { get; set; }
    public ForwardingConstraints ForwardingConstraints { get; set; }
}
