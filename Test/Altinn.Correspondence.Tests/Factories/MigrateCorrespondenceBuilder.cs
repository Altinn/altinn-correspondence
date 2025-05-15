using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;

namespace Altinn.Correspondence.Tests.Factories
{
    public class MigrateCorrespondenceBuilder
    {
        private MigrateCorrespondenceExt _migratedCorrespondence;
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
                new CorrespondenceStatusEventExt()
                    {
                        Status = CorrespondenceStatusExt.Initialized,
                        StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 5))
                    },
                    new CorrespondenceStatusEventExt()
                    {
                        Status = CorrespondenceStatusExt.Published,
                        StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 6))
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

        public MigrateCorrespondenceBuilder WithStatusEvent(CorrespondenceStatusExt status, DateTime occurred)
        {
            _migratedCorrespondence.EventHistory.Add(new CorrespondenceStatusEventExt()
            {
                Status = status,
                StatusChanged = new DateTimeOffset(occurred)
            });
            return this;
        }
    }
}