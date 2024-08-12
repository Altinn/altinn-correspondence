using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;

namespace Altinn.Correspondece.Tests.Factories;
internal static class InitializeAttachmentFactory
{
    internal static InitializeAttachmentExt BasicAttachment() => new InitializeAttachmentExt()
    {
        ResourceId = "1",
        DataType = "html",
        ExpirationTime = DateTime.UtcNow.AddDays(1),
        Name = "Test file logical name",
        RestrictionName = "Test file restriction name",
        Sender = "0192:986252932",
        SendersReference = "1234",
        FileName = "test-file",
        IsEncrypted = false
    };
}
