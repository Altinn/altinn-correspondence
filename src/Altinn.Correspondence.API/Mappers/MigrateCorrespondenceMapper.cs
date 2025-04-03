using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.InitializeCorrespondence;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Mappers;

internal static class MigrateCorrespondenceMapper
{
    internal static MigrateCorrespondenceRequest MapToRequest(MigrateCorrespondenceExt migrateCorrespondenceExt)
    {
        var correspondence = new CorrespondenceEntity
        {
            Altinn2CorrespondenceId = migrateCorrespondenceExt.Altinn2CorrespondenceId,
            Statuses = [.. migrateCorrespondenceExt.EventHistory.Select(eh => new CorrespondenceStatusEntity() 
            { 
                Status = (CorrespondenceStatus)eh.Status, 
                StatusChanged = eh.StatusChanged, 
                StatusText = eh.StatusText ?? eh.Status.ToString()
            })],
            Notifications = migrateCorrespondenceExt.NotificationHistory.Select(n => new CorrespondenceNotificationEntity() 
            {
                Created = n.NotificationSent,
                NotificationSent = n.NotificationSent,
                NotificationChannel = (NotificationChannel)n.NotificationChannel,
                NotificationTemplate = NotificationTemplate.Altinn2Message,
                NotificationAddress = n.NotificationAddress,
                Altinn2NotificationId = n.Altinn2NotificationId
            }).ToList(),
            ForwardingEvents = migrateCorrespondenceExt.ForwardingHistory.Select(fh => new CorrespondenceForwardingEventEntity()
            {
                ForwardedOnDate = fh.ForwardedOnDate,
                ForwardedByPartyUuid = fh.ForwardedByPartyUuid,
                ForwardedByUserId = fh.ForwardedByUserId,
                ForwardedByUserUuid = fh.ForwardedByUserUuid,
                ForwardedToUserId = fh.ForwardedToUserId,
                ForwardedToUserUuid = fh.ForwardedToUserUuid,
                ForwardingText = fh.ForwardingText,
                ForwardedToEmailAddress = fh.ForwardedToEmail,
                MailboxSupplier = fh.MailboxSupplier                

            }).ToList(),
            SendersReference = migrateCorrespondenceExt.CorrespondenceData.Correspondence.SendersReference,
            Recipient = migrateCorrespondenceExt.CorrespondenceData.Recipients.First(),
            ResourceId = migrateCorrespondenceExt.CorrespondenceData.Correspondence.ResourceId,
            Sender = migrateCorrespondenceExt.CorrespondenceData.Correspondence.Sender,
            MessageSender = migrateCorrespondenceExt.CorrespondenceData.Correspondence.MessageSender,
            RequestedPublishTime = (DateTimeOffset)migrateCorrespondenceExt.CorrespondenceData.Correspondence.RequestedPublishTime,
            AllowSystemDeleteAfter = migrateCorrespondenceExt.CorrespondenceData.Correspondence.AllowSystemDeleteAfter,
            DueDateTime = migrateCorrespondenceExt.CorrespondenceData.Correspondence.DueDateTime,
            PropertyList = migrateCorrespondenceExt.CorrespondenceData.Correspondence.PropertyList,
            ReplyOptions = migrateCorrespondenceExt.CorrespondenceData.Correspondence.ReplyOptions != null ? CorrespondenceReplyOptionsMapper.MapListToEntities(migrateCorrespondenceExt.CorrespondenceData.Correspondence.ReplyOptions) : new List<CorrespondenceReplyOptionEntity>(),
            IgnoreReservation = migrateCorrespondenceExt.CorrespondenceData.Correspondence.IgnoreReservation,
            ExternalReferences = migrateCorrespondenceExt.CorrespondenceData.Correspondence.ExternalReferences != null ? ExternalReferenceMapper.MapListToEntities(migrateCorrespondenceExt.CorrespondenceData.Correspondence.ExternalReferences) : new List<ExternalReferenceEntity>(),
            Created = DateTimeOffset.UtcNow,
            Content = migrateCorrespondenceExt.CorrespondenceData.Correspondence.Content != null ? new CorrespondenceContentEntity
            {
                Language = migrateCorrespondenceExt.CorrespondenceData.Correspondence.Content.Language,
                MessageTitle = migrateCorrespondenceExt.CorrespondenceData.Correspondence.Content.MessageTitle,
                MessageSummary = migrateCorrespondenceExt.CorrespondenceData.Correspondence.Content.MessageSummary,
                MessageBody = migrateCorrespondenceExt.CorrespondenceData.Correspondence.Content.MessageBody,
                Attachments = []
            } : null,
            IsConfirmationNeeded = migrateCorrespondenceExt.CorrespondenceData.Correspondence.IsConfirmationNeeded,
            IsMigrating = migrateCorrespondenceExt.IsMigrating
        };
        
        return new MigrateCorrespondenceRequest()
        {
            CorrespondenceEntity = correspondence,
            Altinn2CorrespondenceId = migrateCorrespondenceExt.Altinn2CorrespondenceId,
            ExistingAttachments = migrateCorrespondenceExt.CorrespondenceData.ExistingAttachments ?? new List<Guid>()
        };
    }

    internal static CorrespondenceMigrationStatusExt MapToExternal(MigrateCorrespondenceResponse migrateCorrespondenceResponse)
    {
        return new CorrespondenceMigrationStatusExt()
        {
            CorrespondenceId = migrateCorrespondenceResponse.CorrespondenceId,
            Altinn2CorrespondenceId = migrateCorrespondenceResponse.Altinn2CorrespondenceId,
            AttachmentStatuses = MapToExternal(migrateCorrespondenceResponse.AttachmentMigrationStatuses)
        };
    }

    private static List<AttachmentMigrationStatusExt>? MapToExternal(List<AttachmentMigrationStatus>? attachmentMigrationStatuses)
    {
        if(attachmentMigrationStatuses == null)
            return null;
            
        return attachmentMigrationStatuses.Select(MapToExternal).ToList();
    }

    private static AttachmentMigrationStatusExt MapToExternal(AttachmentMigrationStatus attachmentMigrationStatus)
    {
        return new AttachmentMigrationStatusExt()
        {
            AttachmentId = attachmentMigrationStatus.AttachmentId,
            Status = (AttachmentStatusExt)attachmentMigrationStatus.AttachmentStatus
        };
    }
}
