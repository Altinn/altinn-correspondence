using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Application.InitializeCorrespondence;
using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Mappers;

internal static class InitializeCorrespondenceMapper
{
    internal static InitializeCorrespondenceRequest MapToRequest(InitializeCorrespondenceExt initializeCorrespondenceExt, List<IFormFile>? attachments, bool isUploadRequest)
    {
        var correspondence = new CorrespondenceEntity
        {
            SendersReference = initializeCorrespondenceExt.SendersReference,
            Recipient = initializeCorrespondenceExt.Recipient,
            ResourceId = initializeCorrespondenceExt.ResourceId,
            Sender = initializeCorrespondenceExt.Sender,
            VisibleFrom = initializeCorrespondenceExt.VisibleFrom,
            AllowSystemDeleteAfter = initializeCorrespondenceExt.AllowSystemDeleteAfter,
            DueDateTime = initializeCorrespondenceExt.DueDateTime,
            PropertyList = initializeCorrespondenceExt.PropertyList,
            ReplyOptions = CorrespondenceReplyOptionsMapper.MapListToEntities(initializeCorrespondenceExt.ReplyOptions),
            IsReservable = initializeCorrespondenceExt.IsReservable,
            Notifications = InitializeCorrespondenceNotificationMapper.MapListToEntities(initializeCorrespondenceExt.Notifications),
            Statuses = new List<CorrespondenceStatusEntity>(),
            Created = DateTimeOffset.UtcNow,
            Content = initializeCorrespondenceExt.Content != null ? new CorrespondenceContentEntity
            {
                Language = initializeCorrespondenceExt.Content.Language,
                MessageTitle = initializeCorrespondenceExt.Content.MessageTitle,
                MessageSummary = initializeCorrespondenceExt.Content.MessageSummary,
                MessageBody = initializeCorrespondenceExt.Content.MessageBody,
                Attachments = InitializeCorrespondenceAttachmentMapper.MapListToEntities(initializeCorrespondenceExt.Content.Attachments)
            } : null,
        };
        return new InitializeCorrespondenceRequest()
        {
            Correspondence = correspondence,
            Attachments = attachments ?? new List<IFormFile>(),
            isUploadRequest = isUploadRequest
        };
    }
}
