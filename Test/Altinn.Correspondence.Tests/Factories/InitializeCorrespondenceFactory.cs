using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;

namespace Altinn.Correspondece.Tests.Factories;
internal static class InitializeCorrespondenceFactory
{
    internal static InitializeCorrespondenceExt BasicCorrespondence() => new InitializeCorrespondenceExt()
    {
        Recipient = "1",
        ResourceId = "1",
        Sender = "8536:031145332",
        SendersReference = "1",
        Content = new InitializeCorrespondenceContentExt()
        {
            Language = "no",
            MessageTitle = "test",
            MessageSummary = "test",
            Attachments = new List<InitializeCorrespondenceAttachmentExt>() {
                new InitializeCorrespondenceAttachmentExt()
                {
                    DataType = "html",
                    Name = "2",
                    RestrictionName = "testFile2",
                    SendersReference = "1234",
                    IntendedPresentation = IntendedPresentationTypeExt.HumanReadable,
                    FileName = "test-fil2e",
                    IsEncrypted = false,
                }
            },
        },
        VisibleFrom = new DateTimeOffset(),
        AllowSystemDeleteAfter = new DateTimeOffset().AddDays(1),
        DueDateTime = new DateTimeOffset().AddDays(1),
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
                SendersReference = "1",
                RequestedSendTime =  new DateTimeOffset().AddDays(1),
            }
        },
        IsReservable = true
    };
}