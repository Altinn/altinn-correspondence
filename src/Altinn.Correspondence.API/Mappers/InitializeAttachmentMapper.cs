using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Application.InitializeAttachmentCommand;
using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Mappers;

internal static class InitializeAttachmentMapper
{
    internal static InitializeAttachmentCommandRequest MapToRequest(InitializeAttachmentExt initializeAttachmentExt)
    {
        var attachment = new AttachmentEntity
        {
            FileName = initializeAttachmentExt.FileName,
            SendersReference = initializeAttachmentExt.SendersReference,
            DataType = initializeAttachmentExt.DataType,
            IntendedPresentation = (IntendedPresentationType)initializeAttachmentExt.IntendedPresentation,
            Checksum = initializeAttachmentExt.Checksum,
            IsEncrypted = initializeAttachmentExt.IsEncrypted,
            Created = DateTimeOffset.UtcNow,
        };
        return new InitializeAttachmentCommandRequest()
        {
            Attachment = attachment
        };
    }
}
