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
        Name = "testFile",
        RestrictionName = "testFile",
        SendersReference = "1234",
        FileName = "test-file",
        IsEncrypted = false
    };
}
