using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Mappers;

internal static class InitializeCorrespondenceAttachmentMapper
{
    private static AttachmentDataLocationType MapDataLocationType(InitializeAttachmentDataLocationTypeExt dataLocationType) =>
        dataLocationType switch
        {
            // Both "new" and "existing correspondence attachment" are stored in Altinn Correspondence storage.
            InitializeAttachmentDataLocationTypeExt.NewCorrespondenceAttachment => AttachmentDataLocationType.AltinnCorrespondenceAttachment,
            InitializeAttachmentDataLocationTypeExt.ExistingCorrespondenceAttachment => AttachmentDataLocationType.AltinnCorrespondenceAttachment,
            InitializeAttachmentDataLocationTypeExt.ExistingExternalStorage => AttachmentDataLocationType.ExternalStorage,
            _ => AttachmentDataLocationType.AltinnCorrespondenceAttachment
        };

    internal static CorrespondenceAttachmentEntity MapToEntity(InitializeCorrespondenceAttachmentExt initializeAttachmentExt, string resourceId, string sender)
    {
        return new CorrespondenceAttachmentEntity
        {
            Created = DateTimeOffset.UtcNow,
            Attachment = new AttachmentEntity
            {
                Created = DateTimeOffset.UtcNow,
                FileName = initializeAttachmentExt.FileName,
                DisplayName = initializeAttachmentExt.DisplayName,
                ResourceId = resourceId,
                Sender = sender,
                ServiceOwnerId = sender.WithoutPrefix(),
                SendersReference = initializeAttachmentExt.SendersReference,
                Checksum = initializeAttachmentExt.Checksum,
                IsEncrypted = initializeAttachmentExt.IsEncrypted,
                DataLocationType = MapDataLocationType(initializeAttachmentExt.DataLocationType),
                ExpirationInDays = initializeAttachmentExt.ExpirationInDays,
            }
        };
    }
}
