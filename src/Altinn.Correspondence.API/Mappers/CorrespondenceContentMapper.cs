using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Core.Models;

namespace Altinn.Correspondence.Mappers;

internal static class CorrespondenceContentMapper
{
    internal static CorrespondenceContentExt MapToExternal(CorrespondenceContentEntity correspondenceContent)
    {
        var content = new CorrespondenceContentExt
        {
            Language = correspondenceContent.Language,
            MessageSummary = correspondenceContent.MessageSummary,
            MessageTitle = correspondenceContent.MessageTitle,
            Attachments = CorrespondenceAttachmentMapper.MapListToExternal(correspondenceContent.Attachments),
        };
        return content;
    }
}
