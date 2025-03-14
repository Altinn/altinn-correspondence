using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.GetAttachmentDetails;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Mappers;

internal static class AttachmentDetailsMapper
{
    internal static AttachmentDetailsExt MapToExternal(GetAttachmentDetailsResponse AttachmentDetails)
    {
        var attachment = new AttachmentDetailsExt
        {
            ResourceId = "1",
            AttachmentId = AttachmentDetails.AttachmentId,
            Status = (AttachmentStatusExt)AttachmentDetails.Status,
            FileName = AttachmentDetails.FileName,
            DisplayName = AttachmentDetails.DisplayName,
            Sender = AttachmentDetails.Sender,
            StatusText = AttachmentDetails.StatusText,
            StatusChanged = AttachmentDetails.StatusChanged,
            DataType = AttachmentDetails.DataType,
            SendersReference = AttachmentDetails.SendersReference,
            StatusHistory = AttachmentStatusMapper.MapToExternal(AttachmentDetails.Statuses),
            CorrespondenceIds = AttachmentDetails.CorrespondenceIds,
            Checksum = AttachmentDetails.Checksum,
        };
        return attachment;
    }
}
