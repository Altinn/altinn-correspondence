using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Core.Models;

namespace Altinn.Correspondence.Mappers;

internal static class InitializeCorrespondencesMapper
{
    internal static InitializeCorrespondencesRequest MapToRequest(BaseCorrespondenceExt initializeCorrespondenceExt, List<string> Recipients, List<IFormFile>? attachments, List<Guid>? existingAttachments, bool isUploadRequest)
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
        return new InitializeCorrespondencesRequest()
        {
            Correspondence = correspondence,
            Attachments = attachments ?? new List<IFormFile>(),
            IsUploadRequest = isUploadRequest,
            ExistingAttachments = existingAttachments ?? new List<Guid>(),
            Recipients = Recipients
        };
    }
}
