using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Common.Constants;

namespace Altinn.Correspondence.Tests.Factories
{
    public class CorrespondenceEntityBuilder
    {
        private CorrespondenceEntity _correspondenceEntity;

        public CorrespondenceEntityBuilder()
        {
            _correspondenceEntity = new CorrespondenceEntity
            {
                Id = Guid.NewGuid(),
                ResourceId = "test-resource-id",
                Recipient = $"{UrnConstants.OrganizationNumberAttribute}:991825827",
                Sender = $"{UrnConstants.OrganizationNumberAttribute}:991825827",
                SendersReference = "test-senders-reference",
                RequestedPublishTime = DateTimeOffset.UtcNow,
                Statuses = new List<CorrespondenceStatusEntity>(),
                ExternalReferences = new List<ExternalReferenceEntity>(),
                Created = DateTimeOffset.UtcNow,
                Content = new CorrespondenceContentEntity
                {
                    Id = Guid.NewGuid(),
                    Language = "nb",
                    MessageTitle = "Default title",
                    MessageSummary = "Default summary",
                    MessageBody = "Default body",
                    Attachments = new List<CorrespondenceAttachmentEntity>()
                }
            };
        }

        public CorrespondenceEntity Build()
        {
            return _correspondenceEntity;
        }

        public CorrespondenceEntityBuilder WithCreated(DateTime created)
        {
            _correspondenceEntity.Created = new DateTimeOffset(created);
            return this;
        }

        public CorrespondenceEntityBuilder WithStatus(CorrespondenceStatus status)
        {
            _correspondenceEntity.Statuses.Add(new CorrespondenceStatusEntity
            {
                Status = status,
                StatusChanged = DateTimeOffset.UtcNow
            });
            return this;
        }

        public CorrespondenceEntityBuilder WithStatus(CorrespondenceStatus status, DateTime statusChanged, Guid partyUuid)
        {
            _correspondenceEntity.Statuses.Add(new CorrespondenceStatusEntity
            {
                Status = status,
                StatusChanged = new DateTimeOffset(statusChanged),
                PartyUuid = partyUuid
            });
            return this;
        }

        public CorrespondenceEntityBuilder WithExternalReference(ReferenceType referenceType, string referenceValue)
        {
            _correspondenceEntity.ExternalReferences.Add(new ExternalReferenceEntity
            {
                ReferenceType = referenceType,
                ReferenceValue = referenceValue
            });
            return this;
        }

        public CorrespondenceEntityBuilder WithMessageTitle(string? messageTitle)
        {
            if (_correspondenceEntity.Content != null)
            {
                _correspondenceEntity.Content.MessageTitle = messageTitle ?? "";
            }
            return this;
        }

        public CorrespondenceEntityBuilder WithAltinn2CorrespondenceId(int altinn2CorrespondenceId)
        {
            _correspondenceEntity.Altinn2CorrespondenceId = altinn2CorrespondenceId;
            return this;
        }

        public CorrespondenceEntityBuilder WithIsMigrating(bool isMigrating)
        {
            _correspondenceEntity.IsMigrating = isMigrating;
            return this;
        }

        public CorrespondenceEntityBuilder WithDialogId(string dialogId)
        {
            if (_correspondenceEntity.ExternalReferences == null)
            {
                _correspondenceEntity.ExternalReferences = new List<ExternalReferenceEntity>();
            }

            _correspondenceEntity.ExternalReferences.Add(new ExternalReferenceEntity
            {
                ReferenceType = ReferenceType.DialogportenDialogId,
                ReferenceValue = dialogId
            });
            
            return this;
        }

        public CorrespondenceEntityBuilder WithAttachment(string attachmentName)
        {
            _correspondenceEntity.Content.Attachments.Add(new CorrespondenceAttachmentEntity
            {
                Id = Guid.NewGuid(),
                Attachment = new AttachmentEntity
                {
                    Id = Guid.NewGuid(),
                    ResourceId = "test-attachment-resource-id",
                    FileName = attachmentName,
                    SendersReference = "test-attachment-senders-reference",
                    Sender = $"{UrnConstants.OrganizationNumberAttribute}:991825827",
                    Created = _correspondenceEntity.Created
                }
            });
            return this;
        }

        public CorrespondenceEntityBuilder WithSingleAltinn2Notification(int altinn2NotificationId, string address, NotificationChannel channel, DateTimeOffset created, DateTimeOffset? sent, bool isReminder = false)
        {
            _correspondenceEntity.Notifications = new List<CorrespondenceNotificationEntity> {
                new CorrespondenceNotificationEntity()
                {
                    CorrespondenceId = _correspondenceEntity.Id,
                    Created = created,
                    NotificationSent = sent,
                    NotificationAddress = address,
                    NotificationChannel = channel,
                    Altinn2NotificationId = altinn2NotificationId,
                    IsReminder = isReminder,
                    NotificationTemplate = NotificationTemplate.Altinn2Message
                }
            };
            return this;
        }

        public CorrespondenceEntityBuilder WithForwardingEvents(List<CorrespondenceForwardingEventEntity> correspondenceForwardingEventEntities)
        {
            _correspondenceEntity.ForwardingEvents = correspondenceForwardingEventEntities;
            foreach (var forwardingEvent in correspondenceForwardingEventEntities)
            {
                forwardingEvent.CorrespondenceId = _correspondenceEntity.Id;
            }

            return this;
        }
    }
}