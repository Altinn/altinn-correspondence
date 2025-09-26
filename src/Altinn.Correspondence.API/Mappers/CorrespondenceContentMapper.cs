using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Mappers;

internal static class CorrespondenceContentMapper
{
    internal static CorrespondenceContentExt MapToExternal(CorrespondenceContentEntity? correspondenceContent)
    {
        if (correspondenceContent is null)
        {
            return new CorrespondenceContentExt
            {
                Language = "no",
                MessageSummary = string.Empty,
                MessageTitle = string.Empty,
                MessageBody = string.Empty,
                Attachments = new List<CorrespondenceAttachmentExt>(),
            };
        }
        var content = new CorrespondenceContentExt
        {
            Language = correspondenceContent.Language,
            MessageSummary = correspondenceContent.MessageSummary,
            MessageTitle = correspondenceContent.MessageTitle,
            MessageBody = correspondenceContent.MessageBody,
            Attachments = CorrespondenceAttachmentMapper.MapListToExternal(correspondenceContent.Attachments),
        };
        return content;
    }
}
