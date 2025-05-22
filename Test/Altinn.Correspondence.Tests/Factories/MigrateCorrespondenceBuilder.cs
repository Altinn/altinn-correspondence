using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.API.Models.Migration;
using Altinn.Correspondence.Common.Constants;

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
            var basicCorrespondence = new MigrateInitializeCorrespondencesExt()
            {
                Correspondence = new MigrateBaseCorrespondenceExt()
                {
                    ResourceId = "1",
                    Sender = $"{UrnConstants.OrganizationNumberAttribute}:991825827",
                    SendersReference = "1",
                    Content = new MigrateInitializeCorrespondenceContentExt()
                    {
                        Language = "nb",
                        MessageTitle = "test",
                        MessageSummary = "# test",
                        MessageBody = "# test body /n __test__ /n **test**/n [test](www.test.no) /n ![test](www.test.no) /n ```test``` /n > test /n - test /n 1. test /n 1. test /n [x] test /n [ ] test /n ## test /n ### test /n #### test /n ##### test /n ###### test /n + test list /n - test list /n * list element",
                    },
                    RequestedPublishTime = DateTimeOffset.UtcNow,
                    PropertyList = new Dictionary<string, string>(){
                        {"deserunt_12", "1"},
                        {"culpa_852", "2"},
                        {"anim5", "3"}
                    },
                    IgnoreReservation = false,
                    IsConfirmationNeeded = false,
                },
                Recipients = new List<string>(){
                    $"{UrnConstants.OrganizationNumberAttribute}:991825827",   // org number
                },
                ExistingAttachments = new List<Guid>(),
            };

            basicCorrespondence.Correspondence.Content.MessageBody = "<html><header>test header</header><body>test body</body></html>";

            _migratedCorrespondence = new()
            {
                CorrespondenceData = basicCorrespondence,
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

        public MigrateCorrespondenceBuilder WithSendersReference(string senderReference)
        {
            _migratedCorrespondence.CorrespondenceData.Correspondence.SendersReference = senderReference;
            return this;
        }
    }
}