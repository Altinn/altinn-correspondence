using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Application.InitializeMultipleCorrespondences;
using Altinn.Correspondence.Core.Models;

namespace Altinn.Correspondence.Mappers;

internal static class InitializeMultipleCorrespondencesMapper
{
    internal static InitializeMultipleCorrespondencesRequest MapToRequest(BaseCorrespondenceExt initializeCorrespondenceExt, List<string> Recipients, List<IFormFile>? attachments, bool isUploadRequest)
    {
        var correspondence = new CorrespondenceEntity
        {
            SendersReference = initializeCorrespondenceExt.SendersReference,
            Recipient = null,
            ResourceId = initializeCorrespondenceExt.ResourceId,
            Sender = initializeCorrespondenceExt.Sender,
            VisibleFrom = initializeCorrespondenceExt.VisibleFrom,
            AllowSystemDeleteAfter = initializeCorrespondenceExt.AllowSystemDeleteAfter,
            DueDateTime = initializeCorrespondenceExt.DueDateTime,
            PropertyList = initializeCorrespondenceExt.PropertyList,
            ReplyOptions = initializeCorrespondenceExt.ReplyOptions != null ? CorrespondenceReplyOptionsMapper.MapListToEntities(initializeCorrespondenceExt.ReplyOptions) : new List<CorrespondenceReplyOptionEntity>(),
            IsReservable = initializeCorrespondenceExt.IsReservable,
            Notifications = initializeCorrespondenceExt.Notifications != null ? InitializeCorrespondenceNotificationMapper.MapListToEntities(initializeCorrespondenceExt.Notifications) : new List<CorrespondenceNotificationEntity>(),
            ExternalReferences = initializeCorrespondenceExt.ExternalReferences != null ? ExternalReferenceMapper.MapListToEntities(initializeCorrespondenceExt.ExternalReferences) : new List<ExternalReferenceEntity>(),
            Statuses = new List<CorrespondenceStatusEntity>(),
            Created = DateTimeOffset.UtcNow,
            Content = initializeCorrespondenceExt.Content != null ? new CorrespondenceContentEntity
            {
                Language = initializeCorrespondenceExt.Content.Language,
                MessageTitle = initializeCorrespondenceExt.Content.MessageTitle,
                MessageSummary = initializeCorrespondenceExt.Content.MessageSummary,
                MessageBody = initializeCorrespondenceExt.Content.MessageBody,
                Attachments = initializeCorrespondenceExt.Content.Attachments.Select(
                    attachment => InitializeCorrespondenceAttachmentMapper.MapToEntity(attachment, initializeCorrespondenceExt.ResourceId, initializeCorrespondenceExt.Sender)
                ).ToList()
            } : null,
        };
        return new InitializeMultipleCorrespondencesRequest()
        {
            Correspondence = correspondence,
            Attachments = attachments ?? new List<IFormFile>(),
            isUploadRequest = isUploadRequest,
            Recipients = Recipients
        };
    }
}
