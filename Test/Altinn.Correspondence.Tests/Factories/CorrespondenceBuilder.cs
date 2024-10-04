using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;

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
                    VisibleFrom = DateTimeOffset.UtcNow,
                    DueDateTime = DateTimeOffset.UtcNow.AddDays(2),
                    AllowSystemDeleteAfter = DateTimeOffset.UtcNow.AddDays(3),
                    PropertyList = new Dictionary<string, string>(){
                        {"deserunt_12", "1"},
                        {"culpa_852", "2"},
                        {"anim5", "3"}
                    },
                    IsReservable = true
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
        public CorrespondenceBuilder WithTitle(string title)
        {
            _correspondence.Correspondence.Content!.MessageTitle = title;
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
        public CorrespondenceBuilder WithNotifications()
        {
            _correspondence.Correspondence.Notification = new InitializeCorrespondenceNotificationExt()
            {
                NotificationTemplate = NotificationTemplateExt.GenericAltinnMessage,
                NotificationChannel = NotificationChannelExt.Email,
                SendersReference = "0192:986252932",
                RequestedSendTime = DateTime.UtcNow.AddDays(1),
                SendReminder = true,
            };
            _correspondence.Correspondence.ReplyOptions = new List<CorrespondenceReplyOptionExt>(){
                new CorrespondenceReplyOptionExt()
                {
                    LinkURL = "www.test.no",
                    LinkText = "test"
                },
                new CorrespondenceReplyOptionExt()
                {
                    LinkURL = "test.no",
                    LinkText = "test"
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
        public CorrespondenceBuilder WithAttachmentMetaData(List<InitializeCorrespondenceAttachmentExt> attachmentMetaData)
        {
            _correspondence.Correspondence.Content!.Attachments = attachmentMetaData;
            return this;
        }
        public CorrespondenceBuilder WithExistingAttachments(string attachmentId)
        {
            _correspondence.ExistingAttachments = new List<Guid>(){
                Guid.Parse(attachmentId)
            };
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

        public CorrespondenceBuilder WithMessageBody(string? messageBody)
        {
            _correspondence.Correspondence.Content!.MessageBody = messageBody;
            return this;
        }
        public CorrespondenceBuilder WithDueDateTime(DateTimeOffset dueDateTime)
        {
            _correspondence.Correspondence.DueDateTime = dueDateTime;
            return this;
        }
        public CorrespondenceBuilder WithVisibleFrom(DateTimeOffset dueDateTime)
        {
            _correspondence.Correspondence.VisibleFrom = dueDateTime;
            return this;
        }
        public CorrespondenceBuilder WithAllowSystemDeleteAfter(DateTimeOffset dueDateTime)
        {
            _correspondence.Correspondence.AllowSystemDeleteAfter = dueDateTime;
            return this;
        }
    }
}


