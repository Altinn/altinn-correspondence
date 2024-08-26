using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;

namespace Altinn.Correspondece.Tests.Factories;
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
}
