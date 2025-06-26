using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using System.Text.Json;

namespace Altinn.Correspondence.Tests.Factories
{
    public class MigrateCorrespondenceBuilder
    {
        private MigrateCorrespondenceExt _migratedCorrespondence;
        private Guid _defaultUserPartyUuid = new Guid("11112222333344445555666677778888");
        public MigrateCorrespondenceExt Build()
        {
            return _migratedCorrespondence;
        }
        public MigrateCorrespondenceBuilder CreateMigrateCorrespondence()
        {
            InitializeCorrespondencesExt basicCorrespondence = new CorrespondenceBuilder().CreateCorrespondence().Build();
            MigrateInitializeCorrespondencesExt migrateCorrespondence = new()
            {
                Correspondence = basicCorrespondence.Correspondence,
                Recipients = basicCorrespondence.Recipients,
                ExistingAttachments = basicCorrespondence.ExistingAttachments,
                IdempotentKey = basicCorrespondence.IdempotentKey
            };

            migrateCorrespondence.Correspondence.Content.MessageBody = "<html><header>test header</header><body>test body</body></html>";

            _migratedCorrespondence = new()
            {
                CorrespondenceData = migrateCorrespondence,
                Altinn2CorrespondenceId = (new Random().Next()),
                EventHistory =
            [
                new MigrateCorrespondenceStatusEventExt()
                    {
                        Status = CorrespondenceStatusExt.Initialized,
                        StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 5)),
                        EventUserPartyUuid = _defaultUserPartyUuid
                    },
                    new MigrateCorrespondenceStatusEventExt()
                    {
                        Status = CorrespondenceStatusExt.Published,
                        StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 6)),
                        EventUserPartyUuid = _defaultUserPartyUuid
                    }
                ],
                IsMigrating = true
            };

            return this;
        }

        public MigrateCorrespondenceBuilder WithMessageSender(string messageSender)
        {
            _migratedCorrespondence.CorrespondenceData.Correspondence.MessageSender = messageSender;
            return this;
        }

        public MigrateCorrespondenceBuilder WithAltinn2CorrespondenceId(int altinn2CorrespondenceId)
        {
            _migratedCorrespondence.Altinn2CorrespondenceId = altinn2CorrespondenceId;
            return this;
        }

        public MigrateCorrespondenceBuilder WithIsMigrating(bool isMigrating)
        {
            _migratedCorrespondence.IsMigrating = isMigrating;
            return this;
        }

        public MigrateCorrespondenceBuilder WithExistingAttachments(List<Guid> attachmentIds)
        {
            _migratedCorrespondence.CorrespondenceData.ExistingAttachments = attachmentIds;
            return this;
        }

        public MigrateCorrespondenceBuilder WithCreatedAt(DateTimeOffset createdAt)
        {
            _migratedCorrespondence.Created = createdAt;
            return this;
        }

        public MigrateCorrespondenceBuilder WithRecipient(string recipient)
        {
            _migratedCorrespondence.CorrespondenceData.Recipients.Clear();
            _migratedCorrespondence.CorrespondenceData.Recipients.Add(recipient);
            return this;
        }

        public MigrateCorrespondenceBuilder WithResourceId(string resourceId)
        {
            _migratedCorrespondence.CorrespondenceData.Correspondence.ResourceId= resourceId;
            return this;
        }

        public MigrateCorrespondenceBuilder WithStatusEvent(CorrespondenceStatusExt status, DateTime occurred, Guid? userPartyUuid = null)
        {
            if (userPartyUuid == null)
            {
                userPartyUuid = _defaultUserPartyUuid;
            }

            _migratedCorrespondence.EventHistory.Add(new MigrateCorrespondenceStatusEventExt()
            {
                Status = status,
                StatusChanged = new DateTimeOffset(occurred),
                EventUserPartyUuid = (Guid)userPartyUuid
            });
            return this;
        }

        public MigrateCorrespondenceBuilder WithSendersReference(string senderReference)
        {
            _migratedCorrespondence.CorrespondenceData.Correspondence.SendersReference = senderReference;
            return this;
        }
    }
}