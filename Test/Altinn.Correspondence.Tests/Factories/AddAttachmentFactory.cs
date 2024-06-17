using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;

namespace Altinn.Correspondence.Tests.Factories
{
    internal static class AddAttachmentFactory
    {
        internal static AddAttachmentRequest CreateAddAttachmentRequest(Guid? attachmentId)
        {
            return new AddAttachmentRequest
            {
                AttachmentId = (Guid)attachmentId,
                Metadata = new InitializeCorrespondenceAttachmentExt()
                {
                    DataType = "pdf",
                    Name = "2",
                    RestrictionName = "testFile2",
                    SendersReference = "1234",
                    IntendedPresentation = IntendedPresentationTypeExt.MachineReadable,
                    FileName = "test-fil2e",
                    IsEncrypted = false,
                }
            };
        }
    }
}
