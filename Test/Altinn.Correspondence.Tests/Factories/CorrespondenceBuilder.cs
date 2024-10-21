using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Tests.Factories
{
    public class CorrespondenceBuilder
    {
        private InitializeCorrespondencesExt _correspondence;
        public InitializeCorrespondencesExt Build()
        {
            return _correspondence;
        }
        public CorrespondenceBuilder CreateCorrespondence()
        {
            _correspondence = new InitializeCorrespondencesExt()
            {
                Correspondence = new BaseCorrespondenceExt()
                {
                    ResourceId = "1",
                    Sender = "0192:991825827",
                    SendersReference = "1",
                    Content = new InitializeCorrespondenceContentExt()
                    {
                        Language = "no",
                        MessageTitle = "test",
                        MessageSummary = "# test",
                        MessageBody = "# test body /n __test__ /n **test**/n [test](www.test.no) /n ![test](www.test.no) /n ```test``` /n > test /n - test /n 1. test /n 1. test /n [x] test /n [ ] test /n ## test /n ### test /n #### test /n ##### test /n ###### test /n + test list /n - test list /n * list element",
                    },
                    RequestedPublishTime = DateTimeOffset.UtcNow,
                    DueDateTime = DateTimeOffset.UtcNow.AddDays(2),
                    AllowSystemDeleteAfter = DateTimeOffset.UtcNow.AddDays(3),
                    PropertyList = new Dictionary<string, string>(){
                        {"deserunt_12", "1"},
                        {"culpa_852", "2"},
                        {"anim5", "3"}
                    },
                    IgnoreReservation = false
                },
                Recipients = new List<string>(){
                    "0192:991825827",   // org number
                },
                ExistingAttachments = new List<Guid>(),
            };

            return this;
        }
        public CorrespondenceBuilder WithResourceId(string resourceId)
        {
            _correspondence.Correspondence.ResourceId = resourceId;
            return this;
        }
        public CorrespondenceBuilder WithMessageTitle(string title)
        {
            _correspondence.Correspondence.Content!.MessageTitle = title;
            return this;
        }
        public CorrespondenceBuilder WithMessageBody(string? messageBody)
        {
            _correspondence.Correspondence.Content!.MessageBody = messageBody;
            return this;
        }
        public CorrespondenceBuilder WithMessageSummary(string? messageBody)
        {
            _correspondence.Correspondence.Content!.MessageSummary = messageBody;
            return this;
        }
        public CorrespondenceBuilder WithAttachments()
        {
            _correspondence.Correspondence.Content!.Attachments = new List<InitializeCorrespondenceAttachmentExt>() {
                new InitializeCorrespondenceAttachmentExt()
                {
                    DataType = "html",
                    Name = "2",
                    RestrictionName = "testFile2",
                    SendersReference = "1234",
                    FileName = "test-fil2e",
                    IsEncrypted = false,
                }
            };
            return this;
        }
        public CorrespondenceBuilder WithExternalReference()
        {
            _correspondence.Correspondence.ExternalReferences = new List<ExternalReferenceExt>(){
                new ExternalReferenceExt()
                {
                    ReferenceValue = "1",
                    ReferenceType = ReferenceTypeExt.AltinnBrokerFileTransfer
                },
                new ExternalReferenceExt()
                {
                    ReferenceValue = "2",
                    ReferenceType = ReferenceTypeExt.DialogportenProcessId
                }
            };
            return this;
        }
        public CorrespondenceBuilder WithAttachments(List<InitializeCorrespondenceAttachmentExt> attachmentMetaData)
        {
            _correspondence.Correspondence.Content!.Attachments = attachmentMetaData;
            return this;
        }
        public CorrespondenceBuilder WithExistingAttachments(List<Guid> attachmentIds)
        {
            _correspondence.ExistingAttachments = attachmentIds;
            return this;
        }
        public CorrespondenceBuilder WithSender(string sender)
        {
            _correspondence.Correspondence.Sender = sender;
            return this;
        }
        public CorrespondenceBuilder WithRecipients(List<string> recipients)
        {
            _correspondence.Recipients = recipients;
            return this;
        }
        public CorrespondenceBuilder WithDueDateTime(DateTimeOffset dueDateTime)
        {
            _correspondence.Correspondence.DueDateTime = dueDateTime;
            return this;
        }
        public CorrespondenceBuilder WithRequestedPublishTime(DateTimeOffset? requestedPublishTime)
        {
            _correspondence.Correspondence.RequestedPublishTime = requestedPublishTime;
            return this;
        }
        public CorrespondenceBuilder WithAllowSystemDeleteAfter(DateTimeOffset dueDateTime)
        {
            _correspondence.Correspondence.AllowSystemDeleteAfter = dueDateTime;
            return this;
        }
        public CorrespondenceBuilder WithNotificationTemplate(NotificationTemplateExt notificationTemplate)
        {
            _correspondence.Correspondence.Notification ??= new InitializeCorrespondenceNotificationExt()
            {
                NotificationTemplate = notificationTemplate,
                SendReminder = true
            };
            return this;
        }
        public CorrespondenceBuilder WithNotificationChannel(NotificationChannelExt notificationChannel)
        {
            _correspondence.Correspondence.Notification!.NotificationChannel = notificationChannel;
            return this;
        }
        public CorrespondenceBuilder WithReminderNotificationChannel(NotificationChannelExt notificationChannel)
        {
            _correspondence.Correspondence.Notification!.ReminderNotificationChannel= notificationChannel;
            return this;
        }
        public CorrespondenceBuilder WithEmailContent()
        {
            _correspondence.Correspondence.Notification!.EmailBody = "test";
            _correspondence.Correspondence.Notification!.EmailSubject = "test";
            return this;
        }
        public CorrespondenceBuilder WithEmailReminder()
        {
            _correspondence.Correspondence.Notification!.ReminderEmailBody = "test";
            _correspondence.Correspondence.Notification!.ReminderEmailSubject = "test";
            return this;
        }
        public CorrespondenceBuilder WithSmsContent()
        {
            _correspondence.Correspondence.Notification!.SmsBody= "test";
            return this;
        }
        public CorrespondenceBuilder WithSmsReminder()
        {
            _correspondence.Correspondence.Notification!.ReminderSmsBody = "test";
            return this;
        }
        public CorrespondenceBuilder WithoutSendReminder()
        {
            _correspondence.Correspondence.Notification!.SendReminder = false;
            return this;
        }
        public static CorrespondenceEntity CorrespondenceEntityWithNotifications()
        {
            return new CorrespondenceEntity()
            {
                ResourceId = "1",
                Sender = "0192:991825827",
                Recipient = "0192:991825827",
                SendersReference = "1",
                RequestedPublishTime = DateTimeOffset.UtcNow,
                Statuses = new List<CorrespondenceStatusEntity>(),
                Created = DateTimeOffset.UtcNow,
                Notifications = new List<CorrespondenceNotificationEntity>()
                {
                    new CorrespondenceNotificationEntity()
                    {
                        Created = DateTimeOffset.UtcNow,
                        NotificationOrderId = Guid.NewGuid(),
                        RequestedSendTime = DateTimeOffset.UtcNow.AddDays(1),
                        NotificationTemplate = new Core.Models.Enums.NotificationTemplate(),
                        NotificationChannel = new Core.Models.Enums.NotificationChannel(),
                    }
                }
            };
        }
    }
}


