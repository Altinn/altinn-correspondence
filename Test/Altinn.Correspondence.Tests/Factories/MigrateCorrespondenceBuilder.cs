using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Models.Entities;

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

        public MigrateCorrespondenceBuilder WithAttachments()
        {
            _migratedCorrespondence.CorrespondenceData.Correspondence.Content!.Attachments = new List<InitializeCorrespondenceAttachmentExt>() {
                new InitializeCorrespondenceAttachmentExt()
                {
                    SendersReference = "1234",
                    FileName = "test-fil2e.txt",
                    IsEncrypted = false,
                }
            };
            return this;
        }
    }
}