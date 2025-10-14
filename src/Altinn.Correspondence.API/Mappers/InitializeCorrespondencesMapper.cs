using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application;
using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using OneOf;
using System.Text.Json;

namespace Altinn.Correspondence.Mappers;

internal static class InitializeCorrespondencesMapper
{
    internal static OneOf<InitializeCorrespondencesRequest, Error> MapToRequest(InitializeCorrespondencesExt request, List<IFormFile>? attachments = null)
    {
        var rawRequest = JsonSerializer.Serialize(request);

        var correspondence = new CorrespondenceEntity
        {
            SendersReference = request.Correspondence.SendersReference,
            Recipient = null,
            ResourceId = request.Correspondence.ResourceId.WithoutPrefix(),
            Sender = UrnConstants.PlaceholderSender, // This is not required anymore from caller, but it still has to be set to a valid format
            ServiceOwnerId = null, // Will be populated by the handler after determining service owner from ResourceRegistry
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
                Language = string.IsNullOrWhiteSpace(request.Correspondence.Content.Language) ? "nb" : request.Correspondence.Content.Language,
                MessageTitle = request.Correspondence.Content.MessageTitle,
                MessageSummary = request.Correspondence.Content.MessageSummary ?? string.Empty,
                MessageBody = request.Correspondence.Content.MessageBody,
                Attachments = request.Correspondence.Content.Attachments.Select(
                    attachment => InitializeCorrespondenceAttachmentMapper.MapToEntity(attachment, request.Correspondence.ResourceId, request.Correspondence.Sender)
                ).ToList()
            } : null,
            IsConfirmationNeeded = request.Correspondence.IsConfirmationNeeded,
            IsConfidential = request.Correspondence.IsConfidential,
            OriginalRequest = rawRequest
        };
        NotificationRequest? correspondenceNotification = null;
        if (request.Correspondence.Notification != null)
        {
            var notificationResult = InitializeCorrespondenceNotificationMapper.MapToRequest(request.Correspondence.Notification);
            if (notificationResult.IsT1)
            {
                return notificationResult.AsT1;
            }
            correspondenceNotification = notificationResult.AsT0;
        }
        return new InitializeCorrespondencesRequest()
        {
            Correspondence = correspondence,
            Attachments = attachments ?? new List<IFormFile>(),
            ExistingAttachments = request.ExistingAttachments ?? new List<Guid>(),
            Recipients = request.Recipients,
            Notification = correspondenceNotification,
            IdempotentKey = request.IdempotentKey
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
