using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Core.Models.Entities;
using System.Text.Json;

namespace Altinn.Correspondence.Mappers;

internal static class InitializeCorrespondencesMapper
{
    internal static InitializeCorrespondencesRequest MapToRequest(InitializeCorrespondencesExt request, List<IFormFile>? attachments = null)
    {
        var rawRequest = JsonSerializer.Serialize(request);

        var correspondence = new CorrespondenceEntity
        {
            SendersReference = request.Correspondence.SendersReference,
            Recipient = null,
            ResourceId = request.Correspondence.ResourceId,
            Sender = request.Correspondence.Sender,
            MessageSender = request.Correspondence.MessageSender,
            RequestedPublishTime = request.Correspondence.RequestedPublishTime ?? DateTimeOffset.UtcNow,
            AllowSystemDeleteAfter = request.Correspondence.AllowSystemDeleteAfter,
            DueDateTime = request.Correspondence.DueDateTime,
            PropertyList = request.Correspondence.PropertyList,
            ReplyOptions = request.Correspondence.ReplyOptions != null ? CorrespondenceReplyOptionsMapper.MapListToEntities(request.Correspondence.ReplyOptions) : new List<CorrespondenceReplyOptionEntity>(),
            IgnoreReservation = request.Correspondence.IgnoreReservation,
            ExternalReferences = request.Correspondence.ExternalReferences != null ? ExternalReferenceMapper.MapListToEntities(request.Correspondence.ExternalReferences) : new List<ExternalReferenceEntity>(),
            Statuses = new List<CorrespondenceStatusEntity>(),
            Created = DateTimeOffset.UtcNow,
            Content = request.Correspondence.Content != null ? new CorrespondenceContentEntity
            {
                Language = request.Correspondence.Content.Language,
                MessageTitle = request.Correspondence.Content.MessageTitle,
                MessageSummary = request.Correspondence.Content.MessageSummary,
                MessageBody = request.Correspondence.Content.MessageBody,
                Attachments = request.Correspondence.Content.Attachments.Select(
                    attachment => InitializeCorrespondenceAttachmentMapper.MapToEntity(attachment, request.Correspondence.ResourceId, request.Correspondence.Sender)
                ).ToList()
            } : null,
            IsConfirmationNeeded = request.Correspondence.IsConfirmationNeeded,
            OriginalRequest = rawRequest
        };
        return new InitializeCorrespondencesRequest()
        {
            Correspondence = correspondence,
            Attachments = attachments ?? new List<IFormFile>(),
            ExistingAttachments = request.ExistingAttachments ?? new List<Guid>(),
            Recipients = request.Recipients,
            Notification = request.Correspondence.Notification != null ? InitializeCorrespondenceNotificationMapper.MapToRequest(request.Correspondence.Notification) : null,
            IdempotentKey = request.IdempotentKey
        };
    }

    // TODO: Remove this method when we have migrated/tested the new mapper
    internal static InitializeCorrespondencesRequest MapToRequest(BaseCorrespondenceExt initializeCorrespondenceExt, List<string> Recipients, List<IFormFile>? attachments, List<Guid>? existingAttachments, Guid? idempotentKey, string rawRequest)
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
            IsConfirmationNeeded = initializeCorrespondenceExt.IsConfirmationNeeded,
            OriginalRequest = rawRequest
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
