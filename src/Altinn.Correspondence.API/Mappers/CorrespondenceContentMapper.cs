using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Core.Models;

namespace Altinn.Correspondence.Mappers;
internal static class CorrespondenceContentMapper
{
    internal static CorrespondenceContentExt MapToExternal(CorrespondenceContentEntity correspondenceContentEntity)
    {
        var Correspondence = new CorrespondenceContentExt
        {
            Language = correspondenceContentEntity.Language,
            MessageSummary = correspondenceContentEntity.MessageSummary,
            MessageTitle = correspondenceContentEntity.MessageTitle,
            AttachmentIds = correspondenceContentEntity.Attachments.Select(entity => entity.AttachmentId).ToList(),
            Attachments = correspondenceContentEntity.Attachments.Select(entity => new CorrespondenceAttachmentOverviewExt() { 
                DataType = entity.DataType,
                IntendedPresentation = entity.IntendedPresentation == Core.Models.Enums.IntendedPresentationType.HumanReadable ? IntendedPresentationTypeExt.HumanReadable : IntendedPresentationTypeExt.MachineReadable,
                Name = entity.Name,
                SendersReference = entity.SendersReference,
                AttachmentId = entity.AttachmentId,
                Checksum = entity.Checksum,
                CreatedDateTime = DateTime.Now, // TODO, need to store this with the entity
                FileName = entity.Name, // TODO, need to be added to entity
                DataLocationType = entity.DataLocationType == Core.Models.Enums.AttachmentDataLocationType.AltinnCorrespondenceAttachment ? AttachmentDataLocationTypeExt.AltinnCorrespondenceAttachment : AttachmentDataLocationTypeExt.ExternalStorage,
                DataLocationUrl = entity.DataLocationUrl,
                IsEncrypted = entity.IsEncrypted,
                RestrictionName = entity.RestrictionName
            }).ToList(),
        };
        return Correspondence;
    }
}