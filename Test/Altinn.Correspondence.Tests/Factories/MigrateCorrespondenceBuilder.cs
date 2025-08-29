using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using System.Text.Json;

namespace Altinn.Correspondence.Tests.Factories
{
    public class MigrateCorrespondenceBuilder
    {
        private MigrateCorrespondenceExt _migratedCorrespondence;
        private Guid _defaultUserPartyUuid = new Guid("358C48B4-74A7-461F-A86F-48801DEEC920");
        private Guid _defaultUserUuid = new Guid("2607D808-29EC-4BD8-B89F-B9D14BDE634C");
        
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
            
            migrateCorrespondence.Correspondence.Published = new DateTimeOffset(new DateTime(2024, 1, 6));

            _migratedCorrespondence = new()
            {
                CorrespondenceData = migrateCorrespondence,
                Created = new DateTimeOffset(new DateTime(2024, 1, 5)),
                
                Altinn2CorrespondenceId = (new Random().Next()),
                EventHistory =
            [
                new MigrateCorrespondenceStatusEventExt()
                    {
                        Status = MigrateCorrespondenceStatusExt.Initialized,
                        StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 5)),
                        EventUserPartyUuid = _defaultUserPartyUuid,
                        EventUserUuid = _defaultUserUuid

                    },
                    new MigrateCorrespondenceStatusEventExt()
                    {
                        Status = MigrateCorrespondenceStatusExt.Published,
                        StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 6)),
                        EventUserPartyUuid = _defaultUserPartyUuid,
                        EventUserUuid = _defaultUserUuid
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

        public MigrateCorrespondenceBuilder WithSummary(string summary)
        {
            _migratedCorrespondence.CorrespondenceData.Correspondence.Content.MessageSummary = summary;
            return this;
        }


        public MigrateCorrespondenceBuilder WithDueAt(DateTime dueAt)
        {
            _migratedCorrespondence.CorrespondenceData.Correspondence.DueDateTime= new DateTimeOffset(dueAt);
            return this;
        }

        public MigrateCorrespondenceBuilder WithStatusEvent(MigrateCorrespondenceStatusExt status, DateTime occurred, Guid? userPartyUuid = null, Guid? userUuid = null)
        {
            if (userPartyUuid == null)
            {
                userPartyUuid = _defaultUserPartyUuid;
            }

            if (userUuid == null)
            {
                userUuid = _defaultUserUuid;
            }

            _migratedCorrespondence.EventHistory.Add(new MigrateCorrespondenceStatusEventExt()
            {
                Status = status,
                StatusChanged = new DateTimeOffset(occurred),
                EventUserPartyUuid = (Guid)userPartyUuid,
                EventUserUuid = (Guid)userUuid
            });
            return this;
        }

        public MigrateCorrespondenceBuilder WithSendersReference(string senderReference)
        {
            _migratedCorrespondence.CorrespondenceData.Correspondence.SendersReference = senderReference;
            return this;
        }

        public MigrateCorrespondenceBuilder WithNotificationHistoryEvents()
        {
            var published = _migratedCorrespondence.CorrespondenceData.Correspondence.Published;
            _migratedCorrespondence.NotificationHistory =
            [
                new MigrateCorrespondenceNotificationExt()
                {
                    Altinn2NotificationId = 1,
                    NotificationAddress = "testemail@altinn.no",
                    NotificationChannel = NotificationChannelExt.Email,
                    NotificationSent = published.Value.AddHours(1),
                    IsReminder = false
                },
                new MigrateCorrespondenceNotificationExt()
                {
                    Altinn2NotificationId = 2,
                    NotificationAddress = "testemail2@altinn.no",
                    NotificationChannel = NotificationChannelExt.Email,
                    NotificationSent = published.Value.AddHours(2),
                    IsReminder = false
                },
                new MigrateCorrespondenceNotificationExt()
                {
                    Altinn2NotificationId = 3,
                    NotificationAddress = "testemail3@altinn.no",
                    NotificationChannel = NotificationChannelExt.Email,
                    NotificationSent = published.Value.AddHours(3),
                    IsReminder = false
                },
                new MigrateCorrespondenceNotificationExt()
                {
                    Altinn2NotificationId = 4,
                    NotificationAddress = "testemail4@altinn.no",
                    NotificationChannel = NotificationChannelExt.Email,
                    NotificationSent = published.Value.AddHours(4),
                    IsReminder = false
                },
                new MigrateCorrespondenceNotificationExt()
                {
                    Altinn2NotificationId = 5,
                    NotificationAddress = "123456789",
                    NotificationChannel = NotificationChannelExt.Sms,
                    NotificationSent = published.Value.AddDays(1).AddHours(1),
                    IsReminder = false
                },
                new MigrateCorrespondenceNotificationExt()
                {
                    Altinn2NotificationId = 6,
                    NotificationAddress = "223456789",
                    NotificationChannel = NotificationChannelExt.Sms,
                    NotificationSent = published.Value.AddDays(1).AddHours(2),
                    IsReminder = false
                },
                new MigrateCorrespondenceNotificationExt()
                {
                    Altinn2NotificationId = 7,
                    NotificationAddress = "323456789",
                    NotificationChannel = NotificationChannelExt.Sms,
                    NotificationSent = published.Value.AddDays(1).AddHours(3),
                    IsReminder = false
                },
                new MigrateCorrespondenceNotificationExt()
                {
                    Altinn2NotificationId = 754537533,
                    NotificationAddress = "423456789",
                    NotificationChannel = NotificationChannelExt.Sms,
                    NotificationSent = published.Value.AddDays(1).AddHours(4),
                    IsReminder = false
                }
            ];
            return this;
        }

        public MigrateCorrespondenceBuilder WithNotificationHistoryEvent(
            int altinn2NotificationId,
            string notificationAddress,
            NotificationChannelExt notificationChannelExt,
            DateTime notificationSent,
            bool isReminder)
        {
            _migratedCorrespondence.NotificationHistory ??= [];
            _migratedCorrespondence.NotificationHistory.Add(new MigrateCorrespondenceNotificationExt { Altinn2NotificationId = altinn2NotificationId, NotificationAddress = notificationAddress, NotificationChannel = notificationChannelExt, NotificationSent = new DateTimeOffset(notificationSent), IsReminder = isReminder });

            return this;
        }

        public MigrateCorrespondenceBuilder WithForwardingEventHistory(List<MigrateCorrespondenceForwardingEventExt> forwardingEventsExt)
        {
            _migratedCorrespondence.ForwardingHistory = forwardingEventsExt;
            return this;
        }
    }
}