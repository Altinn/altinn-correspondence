using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Mappers;

internal static class InitializeCorrespondenceAttachmentMapper
{
    internal static List<CorrespondenceAttachmentEntity> MapListToEntities(List<InitializeCorrespondenceAttachmentExt> initializeAttachmentExt)
    {
        var attachments = new List<CorrespondenceAttachmentEntity>();
        foreach (var attachment in initializeAttachmentExt)
        {
            attachments.Add(MapToEntity(attachment));
        }
        return attachments;
    }
    internal static CorrespondenceAttachmentEntity MapToEntity(InitializeCorrespondenceAttachmentExt initializeAttachmentExt)
    {
        return new CorrespondenceAttachmentEntity
        {
            Name = initializeAttachmentExt.Name,
            SendersReference = initializeAttachmentExt.SendersReference,
            DataType = initializeAttachmentExt.DataType,
            IntendedPresentation = (IntendedPresentationType)initializeAttachmentExt.IntendedPresentation,
            Checksum = initializeAttachmentExt.Checksum,
            IsEncrypted = initializeAttachmentExt.IsEncrypted,
            DataLocationUrl = initializeAttachmentExt.DataLocationUrl,
            DataLocationType = (AttachmentDataLocationType)initializeAttachmentExt.DataLocationType,
        };
    }
}
