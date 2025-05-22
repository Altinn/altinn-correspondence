using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Mappers;

internal static class InitializeCorrespondencesMapper
{
    internal static InitializeCorrespondencesRequest MapToRequest(BaseCorrespondenceExt initializeCorrespondenceExt, List<string> Recipients, List<IFormFile>? attachments, List<Guid>? existingAttachments, Guid? idempotentKey = null)
    {
        var correspondence = new CorrespondenceEntity
        {
            SendersReference = initializeCorrespondenceExt.SendersReference,
            Recipient = null,
            ResourceId = initializeCorrespondenceExt.ResourceId,
            Sender = initializeCorrespondenceExt.Sender,
            MessageSender = initializeCorrespondenceExt.MessageSender,
            RequestedPublishTime = initializeCorrespondenceExt.RequestedPublishTime ?? DateTimeOffset.UtcNow,
            AllowSystemDeleteAfter = initializeCorrespondenceExt.AllowSystemDeleteAfter,
            DueDateTime = initializeCorrespondenceExt.DueDateTime,
            PropertyList = initializeCorrespondenceExt.PropertyList,
            ReplyOptions = initializeCorrespondenceExt.ReplyOptions != null ? CorrespondenceReplyOptionsMapper.MapListToEntities(initializeCorrespondenceExt.ReplyOptions) : new List<CorrespondenceReplyOptionEntity>(),
            IgnoreReservation = initializeCorrespondenceExt.IgnoreReservation,
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
            IsConfirmationNeeded = initializeCorrespondenceExt.IsConfirmationNeeded
        };
        return new InitializeCorrespondencesRequest()
        {
            Correspondence = correspondence,
            Attachments = attachments ?? new List<IFormFile>(),
            ExistingAttachments = existingAttachments ?? new List<Guid>(),
            Recipients = Recipients,
            Notification = initializeCorrespondenceExt.Notification != null ? InitializeCorrespondenceNotificationMapper.MapToRequest(initializeCorrespondenceExt.Notification) : null,
            IdempotentKey = idempotentKey
        };
    }
    internal static InitializeCorrespondencesResponseExt MapToExternal(InitializeCorrespondencesResponse response)
    {
        return new InitializeCorrespondencesResponseExt
        {
            Correspondences = response.Correspondences.Select(correspondence => new InitializedCorrespondencesExt
            {
                CorrespondenceId = correspondence.CorrespondenceId,
                Status = (CorrespondenceStatusExt)correspondence.Status,
                Recipient = correspondence.Recipient,
                Notifications = correspondence.Notifications?.Select(notification => new InitializedCorrespondencesNotificationsExt
                {
                    OrderId = notification.OrderId,
                    IsReminder = notification.IsReminder,
                    Status = (InitializedNotificationStatusExt)notification.Status
                }).ToList()
            }).ToList(),
            AttachmentIds = response.AttachmentIds
        };
    }
}
