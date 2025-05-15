using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;

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
            var basicCorrespondence = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .Build();

            basicCorrespondence.Correspondence.Content.MessageBody = "<html><header>test header</header><body>test body</body></html>";

            _migratedCorrespondence = new()
            {
                CorrespondenceData = basicCorrespondence,
                Altinn2CorrespondenceId = 99911,
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

        public MigrateCorrespondenceBuilder WithStatusEvent(CorrespondenceStatusExt status, DateTime occurred, Guid? userPartyUuid = null)
        {
            if(userPartyUuid == null)
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
    }
}