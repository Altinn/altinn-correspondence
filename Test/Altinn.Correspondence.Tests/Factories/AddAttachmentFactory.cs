using Altinn.Correspondence.API.Models;

namespace Altinn.Correspondence.Tests.Factories
{
    internal static class AddAttachmentFactory
    {
        internal static AddAttachmentRequest CreateAddAttachmentRequest(Guid? attachmentId)
        {
            return new AddAttachmentRequest
            {
                AttachmentId = (Guid)attachmentId
            };
        }
    }
}
