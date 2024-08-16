using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Tests.Helpers;

namespace Altinn.Correspondece.Tests.Factories;
internal static class InitializeCorrespondenceFactory
{
    internal static InitializeCorrespondenceExt BasicCorrespondence() => new InitializeCorrespondenceExt()
    {
        Recipient = "0192:986252932",
        ResourceId = "1",
        Sender = "0192:986252932",
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
                    IsEncrypted = false
                }
            },
        },
        VisibleFrom = DateTime.UtcNow,
        AllowSystemDeleteAfter = DateTime.UtcNow.AddDays(3),
        DueDateTime = DateTime.UtcNow.AddDays(2),
        ExternalReferences = new List<ExternalReferenceExt>(){
            new ExternalReferenceExt()
            {
                ReferenceValue = "1",
                ReferenceType = ReferenceTypeExt.AltinnBrokerFileTransfer
            },
            new ExternalReferenceExt()
            {
                ReferenceValue = "2",
                ReferenceType = ReferenceTypeExt.DialogPortenDialogID
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
        Notifications = new List<InitializeCorrespondenceNotificationExt>(){
            new InitializeCorrespondenceNotificationExt(){
                NotificationTemplate= "test",
                CustomTextToken = "test",
                SendersReference = "0192:986252932",
                RequestedSendTime =  DateTime.UtcNow.AddDays(1),
            }
        },
        IsReservable = true
    };
    internal static InitializeMultipleCorrespondencesExt BasicMultipleCorrespondence(string url) => new InitializeMultipleCorrespondencesExt()
    {
        Correspondence = new BaseCorrespondenceExt()
        {
            ResourceId = "1",
            Sender = "0192:986252932",
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
                        DataLocationUrl = url
                    }
                },
            },
            VisibleFrom = DateTime.UtcNow,
            AllowSystemDeleteAfter = DateTime.UtcNow.AddDays(3),
            DueDateTime = DateTime.UtcNow.AddDays(2),
            ExternalReferences = new List<ExternalReferenceExt>(){
                new ExternalReferenceExt()
                {
                    ReferenceValue = "1",
                    ReferenceType = ReferenceTypeExt.AltinnBrokerFileTransfer
                },
                new ExternalReferenceExt()
                {
                    ReferenceValue = "2",
                    ReferenceType = ReferenceTypeExt.DialogPortenDialogID
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
            Notifications = new List<InitializeCorrespondenceNotificationExt>(){
                new InitializeCorrespondenceNotificationExt(){
                    NotificationTemplate= "test",
                    CustomTextToken = "test",
                    SendersReference = "0192:986252932",
                    RequestedSendTime =  DateTime.UtcNow.AddDays(1),
                }
            },
            IsReservable = true
        },
        Recipients = new List<string>(){
        "0192:986252931",
        "0192:986252932",
        "0192:986252933"
    }
    };

    internal static InitializeCorrespondenceExt BasicCorrespondenceWithFileAttachment()
    {
        var correspondence = BasicCorrespondence();
        correspondence.Content!.Attachments.Add(
            new InitializeCorrespondenceAttachmentExt()
            {
                DataType = "pdf",
                Name = "3",
                RestrictionName = "testFile3",
                SendersReference = "1234",
                FileName = "test-fil3e",
                IsEncrypted = false
            });
        return correspondence;
    }
    internal static InitializeCorrespondenceExt BasicCorrespondenceWithAttachment(List<InitializeCorrespondenceAttachmentExt> attachments)
    {
        var correspondence = BasicCorrespondence();
        correspondence.Content!.Attachments = attachments;
        return correspondence;
    }
    internal static InitializeCorrespondenceExt BasicCorrespondenceWithFileAttachment(string url)
    {
        var correspondence = BasicCorrespondence();
        correspondence.Content!.Attachments = new List<InitializeCorrespondenceAttachmentExt>(){
            new InitializeCorrespondenceAttachmentExt()
            {
                DataType = "pdf",
                Name = "3",
                RestrictionName = "testFile3",
                SendersReference = "1234",
                FileName = "test-fil3e",
                IsEncrypted = false,
                DataLocationUrl = url
            }};
        return correspondence;
    }
    internal static InitializeCorrespondenceExt BasicCorrespondenceWithNoMessageBody()
    {
        var correspondence = BasicCorrespondence();
        correspondence.Content!.MessageBody = null;
        return correspondence;
    }
    internal static InitializeCorrespondenceExt BasicCorrespondenceWithoutAttachments()
    {
        var correspondence = BasicCorrespondence();
        correspondence.Content!.Attachments = new List<InitializeCorrespondenceAttachmentExt>()
        {
        };
        return correspondence;
    }
    internal static InitializeCorrespondenceExt BasicCorrespondenceAlreadyVisible()
    {
        var correspondence = BasicCorrespondence();
        correspondence.VisibleFrom = DateTime.UtcNow.AddDays(-1);
        return correspondence;
    }
    internal static InitializeCorrespondenceExt BasicCorrespondenceWithHtmlInTitle()
    {
        var correspondence = BasicCorrespondence();
        correspondence.Content!.MessageTitle = "<h1>test</h1>";
        return correspondence;
    }
    internal static InitializeCorrespondenceExt BasicCorrespondenceWithMarkdownInTitle()
    {
        var correspondence = BasicCorrespondence();
        correspondence.Content!.MessageTitle = "# test";
        return correspondence;
    }
    internal static InitializeCorrespondenceExt BasicCorrespondenceWithHtmlInSummary()
    {
        var correspondence = BasicCorrespondence();
        correspondence.Content!.MessageSummary = "<h1>test</h1>";

        return correspondence;
    }
    internal static InitializeCorrespondenceExt BasicCorrespondenceWithHtmlInBody()
    {
        var correspondence = BasicCorrespondence();
        correspondence.Content!.MessageBody = "<h1>test</h1>";

        return correspondence;
    }
}
