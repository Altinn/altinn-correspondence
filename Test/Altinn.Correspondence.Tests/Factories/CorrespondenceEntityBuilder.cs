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
                ServiceOwnerId = "123456789", // Default test service owner ID
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

        public CorrespondenceEntityBuilder WithStatus(CorrespondenceStatus status)
        {
            _correspondenceEntity.Statuses.Add(new CorrespondenceStatusEntity
            {
                Status = status,
                StatusChanged = DateTimeOffset.UtcNow
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

        public CorrespondenceEntityBuilder WithServiceOwnerId(string serviceOwnerId)
        {
            _correspondenceEntity.ServiceOwnerId = serviceOwnerId;
            return this;
        }
    }
}