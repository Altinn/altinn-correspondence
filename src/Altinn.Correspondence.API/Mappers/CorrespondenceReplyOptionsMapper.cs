using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Mappers;

internal static class CorrespondenceReplyOptionsMapper
{
    internal static CorrespondenceReplyOptionEntity MapToEntity(CorrespondenceReplyOptionExt correspondenceReplyOptionExt)
    {
        var ReplyOptions = new CorrespondenceReplyOptionEntity
        {
            LinkText = correspondenceReplyOptionExt.LinkText,
            LinkURL = correspondenceReplyOptionExt.LinkURL
        };
        return ReplyOptions;
    }
    internal static CorrespondenceReplyOptionExt MapToExternal(CorrespondenceReplyOptionEntity correspondenceReplyOption)
    {
        var ReplyOptions = new CorrespondenceReplyOptionExt
        {
            LinkText = correspondenceReplyOption.LinkText,
            LinkURL = correspondenceReplyOption.LinkURL
        };
        return ReplyOptions;
    }

    internal static List<CorrespondenceReplyOptionEntity> MapListToEntities(List<CorrespondenceReplyOptionExt> replyOptionsExt)
    {
        var replyOptions = new List<CorrespondenceReplyOptionEntity>();
        foreach (var replyOption in replyOptionsExt)
        {
            replyOptions.Add(MapToEntity(replyOption));
        }
        return replyOptions;
    }
    internal static List<CorrespondenceReplyOptionExt> MapListToExternal(List<CorrespondenceReplyOptionEntity> replyOptions)
    {
        var replyOptionsExt = new List<CorrespondenceReplyOptionExt>();
        foreach (var replyOption in replyOptions)
        {
            replyOptionsExt.Add(MapToExternal(replyOption));
        }
        return replyOptionsExt;
    }
}
