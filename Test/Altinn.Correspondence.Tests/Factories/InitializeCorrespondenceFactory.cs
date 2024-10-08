using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Tests.Factories;
internal static class InitializeCorrespondenceFactory
{
    internal static InitializeCorrespondencesExt BasicCorrespondences(string? url = null) => new InitializeCorrespondencesExt()
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
                Attachments = new List<InitializeCorrespondenceAttachmentExt>() {
                    new InitializeCorrespondenceAttachmentExt()
                    {
                        DataType = "html",
                        Name = "2",
                        RestrictionName = "testFile2",
                        SendersReference = "1234",
                        FileName = "test-fil2e",
                        IsEncrypted = false,
                    }
                },
            },
            VisibleFrom = DateTimeOffset.UtcNow,
            AllowSystemDeleteAfter = DateTimeOffset.UtcNow.AddDays(3),
            DueDateTime = DateTimeOffset.UtcNow.AddDays(2),
            ExternalReferences = new List<ExternalReferenceExt>(){
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
            },
            PropertyList = new Dictionary<string, string>(){
                {"deserunt_12", "1"},
                {"culpa_852", "2"},
                {"anim5", "3"}
            },
            ReplyOptions = new List<CorrespondenceReplyOptionExt>(){
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
            },
            Notification = new InitializeCorrespondenceNotificationExt()
            {
                NotificationTemplate = NotificationTemplateExt.GenericAltinnMessage,
                NotificationChannel = NotificationChannelExt.Email,
                SendersReference = "0192:986252932",
                RequestedSendTime = DateTime.UtcNow.AddDays(1),
                SendReminder = true,
            },
            IgnoreReservation = true
        },
        Recipients = new List<string>(){
            "0192:991825827",
            "0192:986252932",
            "0192:986252933"
        },
        ExistingAttachments = new List<Guid>(),
    };

    internal static InitializeCorrespondencesExt BasicCorrespondenceWithFileAttachment()
    {
        var data = BasicCorrespondences();
        data.Correspondence.Content!.Attachments.Add(
            new InitializeCorrespondenceAttachmentExt()
            {
                DataType = "pdf",
                Name = "3",
                RestrictionName = "testFile3",
                SendersReference = "1234",
                FileName = "test-fil3e",
                IsEncrypted = false
            });
        return data;
    }
    internal static InitializeCorrespondencesExt BasicCorrespondenceWithAttachment(List<InitializeCorrespondenceAttachmentExt> attachments)
    {
        var data = BasicCorrespondences();
        data.Correspondence.Content!.Attachments = attachments;
        return data;
    }
    internal static InitializeCorrespondencesExt BasicCorrespondenceWithNoMessageBody()
    {
        var data = BasicCorrespondences();
        data.Correspondence.Content!.MessageBody = null;
        return data;
    }
    internal static InitializeCorrespondencesExt BasicCorrespondenceWithoutAttachments()
    {
        var data = BasicCorrespondences();
        data.Correspondence.Content!.Attachments = new List<InitializeCorrespondenceAttachmentExt>()
        {
        };
        return data;
    }
    internal static InitializeCorrespondencesExt BasicCorrespondenceAlreadyVisible()
    {
        var data = BasicCorrespondences();
        data.Correspondence.VisibleFrom = DateTime.UtcNow.AddDays(-1);
        return data;
    }
    internal static InitializeCorrespondencesExt BasicCorrespondenceWithHtmlInTitle()
    {
        var data = BasicCorrespondences();
        data.Correspondence.Content!.MessageTitle = "<h1>test</h1>";
        return data;
    }
    internal static InitializeCorrespondencesExt BasicCorrespondenceWithMarkdownInTitle()
    {
        var data = BasicCorrespondences();
        data.Correspondence.Content!.MessageTitle = "# test";
        return data;
    }
    internal static InitializeCorrespondencesExt BasicCorrespondenceWithHtmlInSummary()
    {
        var data = BasicCorrespondences();
        data.Correspondence.Content!.MessageSummary = "<h1>test</h1>";

        return data;
    }
    internal static InitializeCorrespondencesExt BasicCorrespondenceWithHtmlInBody()
    {
        var data = BasicCorrespondences();
        data.Correspondence.Content!.MessageBody = "<h1>test</h1>";

        return data;
    }
    internal static InitializeCorrespondencesExt BasicCorrespondenceWithGenericAltinnEmailNotification()
    {
        var data = BasicCorrespondences();
        data.Correspondence.Notification!.NotificationTemplate = NotificationTemplateExt.GenericAltinnMessage;
        data.Correspondence.Notification!.NotificationChannel = NotificationChannelExt.Email;
        data.Correspondence.Notification!.EmailBody = "test";
        data.Correspondence.Notification!.EmailSubject = "test";
        data.Correspondence.Notification!.SendReminder = true;
        data.Correspondence.Notification!.ReminderEmailBody = "test";
        data.Correspondence.Notification!.ReminderEmailSubject = "test";
        return data;
    }
    internal static InitializeCorrespondencesExt BasicCorrespondenceWithGenericAltinnSmsNotification()
    {
        var data = BasicCorrespondences();
        data.Correspondence.Notification!.NotificationTemplate = NotificationTemplateExt.GenericAltinnMessage;
        data.Correspondence.Notification!.NotificationChannel = NotificationChannelExt.Sms;
        data.Correspondence.Notification!.SmsBody = "test";
        data.Correspondence.Notification!.SendReminder = true;
        data.Correspondence.Notification!.ReminderSmsBody = "test";
        return data;
    }
    internal static InitializeCorrespondencesExt BasicCorrespondenceWithEmptyGenericAltinnEmailNotification()
    {
        var data = BasicCorrespondences();
        data.Correspondence.Notification!.NotificationTemplate = NotificationTemplateExt.GenericAltinnMessage;
        data.Correspondence.Notification!.NotificationChannel = NotificationChannelExt.Email;
        data.Correspondence.Notification!.SendReminder = true;
        return data;
    }
    internal static InitializeCorrespondencesExt BasicCorrespondenceWithEmptyGenericAltinnSmsNotification()
    {
        var data = BasicCorrespondences();
        data.Correspondence.Notification!.NotificationTemplate = NotificationTemplateExt.GenericAltinnMessage;
        data.Correspondence.Notification!.NotificationChannel = NotificationChannelExt.Sms;
        data.Correspondence.Notification!.SendReminder = true;
        return data;
    }
    internal static InitializeCorrespondencesExt BasicCorrespondenceWithPrefferedEmailCustomNotification()
    {
        var data = BasicCorrespondences();
        data.Correspondence.Notification!.NotificationTemplate = NotificationTemplateExt.CustomMessage;
        data.Correspondence.Notification!.NotificationChannel = NotificationChannelExt.EmailPreferred;
        data.Correspondence.Notification!.EmailBody = "test";
        data.Correspondence.Notification!.EmailSubject = "test";
        data.Correspondence.Notification!.SmsBody = "test";
        data.Correspondence.Notification!.SendReminder = true;
        data.Correspondence.Notification!.ReminderEmailBody = "test";
        data.Correspondence.Notification!.ReminderEmailSubject = "test";
        data.Correspondence.Notification!.ReminderSmsBody = "test";
        return data;
    }
    internal static InitializeCorrespondencesExt BasicCorrespondenceWithPrefferedEmailAltinnNotification()
    {
        var data = BasicCorrespondences();
        data.Correspondence.Notification!.NotificationTemplate = NotificationTemplateExt.GenericAltinnMessage;
        data.Correspondence.Notification!.NotificationChannel = NotificationChannelExt.EmailPreferred;
        data.Correspondence.Notification!.SendReminder = true;
        return data;
    }
    internal static InitializeCorrespondencesExt BasicCorrespondenceWithCustomEmailNotification()
    {
        var data = BasicCorrespondences();
        data.Correspondence.Notification!.NotificationTemplate = NotificationTemplateExt.CustomMessage;
        data.Correspondence.Notification!.NotificationChannel = NotificationChannelExt.Email;
        data.Correspondence.Notification!.EmailBody = "test";
        data.Correspondence.Notification!.EmailSubject = "test";
        data.Correspondence.Notification!.SendReminder = true;
        data.Correspondence.Notification!.ReminderEmailBody = "test";
        data.Correspondence.Notification!.ReminderEmailSubject = "test";
        return data;
    }
    internal static InitializeCorrespondencesExt BasicCorrespondenceWithCustomSmsNotification()
    {
        var data = BasicCorrespondences();
        data.Correspondence.Notification!.NotificationTemplate = NotificationTemplateExt.CustomMessage;
        data.Correspondence.Notification!.NotificationChannel = NotificationChannelExt.Sms;
        data.Correspondence.Notification!.SmsBody = "test";
        data.Correspondence.Notification!.SendReminder = true;
        data.Correspondence.Notification!.ReminderSmsBody = "test";
        return data;
    }
    internal static InitializeCorrespondencesExt BasicCorrespondenceWithEmptyCustomEmailNotification()
    {
        var data = BasicCorrespondences();
        data.Correspondence.Notification!.NotificationTemplate = NotificationTemplateExt.CustomMessage;
        data.Correspondence.Notification!.NotificationChannel = NotificationChannelExt.Email;
        data.Correspondence.Notification!.SendReminder = true;
        return data;
    }
    internal static InitializeCorrespondencesExt BasicCorrespondenceWithEmptyCustomSmsNotification()
    {
        var data = BasicCorrespondences();
        data.Correspondence.Notification!.NotificationTemplate = NotificationTemplateExt.CustomMessage;
        data.Correspondence.Notification!.NotificationChannel = NotificationChannelExt.Sms;
        data.Correspondence.Notification!.SendReminder = true;
        return data;
    }
    internal static InitializeCorrespondencesExt BasicCorrespondenceWithPrefferedDataWithMissingData()
    {
        var data = BasicCorrespondences();
        data.Correspondence.Notification!.NotificationTemplate = NotificationTemplateExt.CustomMessage;
        data.Correspondence.Notification!.NotificationChannel = NotificationChannelExt.Email;
        return data;
    }
    internal static InitializeCorrespondencesExt BasicCorrespondenceWithPrefferedDataWithMissingReminderData()
    {
        var data = BasicCorrespondences();
        data.Correspondence.Notification!.NotificationTemplate = NotificationTemplateExt.CustomMessage;
        data.Correspondence.Notification!.NotificationChannel = NotificationChannelExt.Email;
        data.Correspondence.Notification!.EmailBody = "test";
        data.Correspondence.Notification!.EmailSubject = "test";
        data.Correspondence.Notification!.SendReminder = true;
        return data;
    }
    internal static InitializeCorrespondencesExt BasicCorrespondenceWithEmailNotificationWithSmsReminder()
    {
        var data = BasicCorrespondences();
        data.Correspondence.Notification!.NotificationTemplate = NotificationTemplateExt.GenericAltinnMessage;
        data.Correspondence.Notification!.NotificationChannel = NotificationChannelExt.Email;
        data.Correspondence.Notification!.ReminderNotificationChannel = NotificationChannelExt.Sms;
        data.Correspondence.Notification!.EmailBody = "test";
        data.Correspondence.Notification!.EmailSubject = "test";
        data.Correspondence.Notification!.SendReminder = true;
        data.Correspondence.Notification!.ReminderSmsBody = "test";
        return data;
    }
    internal static InitializeCorrespondencesExt BasicCorrespondenceWithEmailNotificationWithSmsPrefferedReminder()
    {
        var data = BasicCorrespondences();
        data.Correspondence.Notification!.NotificationTemplate = NotificationTemplateExt.GenericAltinnMessage;
        data.Correspondence.Notification!.NotificationChannel = NotificationChannelExt.Email;
        data.Correspondence.Notification!.ReminderNotificationChannel = NotificationChannelExt.Sms;
        data.Correspondence.Notification!.EmailBody = "test";
        data.Correspondence.Notification!.EmailSubject = "test";
        data.Correspondence.Notification!.SendReminder = true;
        data.Correspondence.Notification!.ReminderSmsBody = "test";
        data.Correspondence.Notification!.ReminderEmailBody = "test";
        data.Correspondence.Notification!.ReminderEmailSubject = "test";
        return data;
    }
    internal static InitializeCorrespondencesExt BasicCorrespondenceWithSmsNotificationAndEmailReminder()
    {
        var data = BasicCorrespondences();
        data.Correspondence.Notification!.NotificationTemplate = NotificationTemplateExt.GenericAltinnMessage;
        data.Correspondence.Notification!.NotificationChannel = NotificationChannelExt.Sms;
        data.Correspondence.Notification!.ReminderNotificationChannel = NotificationChannelExt.Email;
        data.Correspondence.Notification!.ReminderSmsBody = "test";
        data.Correspondence.Notification!.SendReminder = true;
        data.Correspondence.Notification!.ReminderEmailBody = "test";
        data.Correspondence.Notification!.ReminderEmailSubject = "test";
        return data;
    }
    internal static InitializeCorrespondencesExt BasicCorrespondenceWithSmsNotificationAndEmailPrefferedReminder()
    {
        var data = BasicCorrespondences();
        data.Correspondence.Notification!.NotificationTemplate = NotificationTemplateExt.GenericAltinnMessage;
        data.Correspondence.Notification!.NotificationChannel = NotificationChannelExt.Sms;
        data.Correspondence.Notification!.ReminderNotificationChannel = NotificationChannelExt.EmailPreferred;
        data.Correspondence.Notification!.ReminderSmsBody = "test";
        data.Correspondence.Notification!.SendReminder = true;
        data.Correspondence.Notification!.ReminderEmailBody = "test";
        data.Correspondence.Notification!.ReminderEmailSubject = "test";
        return data;
    }
    internal static CorrespondenceEntity CorrespondenceEntityWithNotifications()
    {
        return new CorrespondenceEntity()
        {
            ResourceId = "1",
            Sender = "0192:991825827",
            Recipient = "0192:991825827",
            SendersReference = "1",
            VisibleFrom = DateTimeOffset.UtcNow,
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
