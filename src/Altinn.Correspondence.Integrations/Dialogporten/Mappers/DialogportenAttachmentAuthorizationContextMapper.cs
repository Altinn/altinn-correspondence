using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Integrations.Dialogporten.Models;

namespace Altinn.Correspondence.Integrations.Dialogporten.Mappers;

internal static class DialogportenAttachmentAuthorizationContextMapper
{
    internal static AuthorizationContext? GetAuthorizationContextForAttachmentResource(CorrespondenceEntity correspondence, string? attachmentResourceId)
    {
        var attachmentResource = attachmentResourceId?.WithoutPrefix();
        if (string.IsNullOrEmpty(attachmentResource) || attachmentResource == correspondence.ResourceId.WithoutPrefix())
        {
            return null;
        }

        return new AuthorizationContext
        {
            ServiceResource = UrnConstants.Resource + ":" + attachmentResource,
            IncludeDialogParty = true,
            DialogPartyChecks = [new DialogPartyCheck { AccessRestriction = "None" }],
            UnauthorizedPresentation = "Disabled"
        };
    }
}
