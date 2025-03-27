using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

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
                Recipient = "test-recipient",
                Sender = "test-sender",
                SendersReference = "test-senders-reference",
                RequestedPublishTime = DateTimeOffset.UtcNow,
                Statuses = new List<CorrespondenceStatusEntity>(),
                ExternalReferences = new List<ExternalReferenceEntity>(),
                Created = DateTimeOffset.UtcNow
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
    }
}