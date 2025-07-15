using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Application.InitializeAttachment;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Mappers;

internal static class InitializeAttachmentMapper
{
    internal static InitializeAttachmentRequest MapToRequest(InitializeAttachmentExt initializeAttachmentExt)
    {
        var attachment = new AttachmentEntity
        {
            ResourceId = initializeAttachmentExt.ResourceId.WithoutPrefix(),
            FileName = initializeAttachmentExt.FileName,
            DisplayName = initializeAttachmentExt.DisplayName,
            Sender = UrnConstants.PlaceholderSender, // Placeholder - will be set by handler from ResourceRegistryService
            SendersReference = initializeAttachmentExt.SendersReference,
            Checksum = initializeAttachmentExt.Checksum,
            IsEncrypted = initializeAttachmentExt.IsEncrypted,
            Created = DateTimeOffset.UtcNow
        };
        return new InitializeAttachmentRequest()
        {
            Attachment = attachment
        };
    }
}
