using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.GetAttachmentDetails;
using Altinn.Correspondence.Core.Models;
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
            Name = AttachmentDetails.Name ?? string.Empty,
            Status = (AttachmentStatusExt)AttachmentDetails.Status,
            DataLocationUrl = AttachmentDetails.DataLocationUrl,
            Sender = AttachmentDetails.Sender,
            RestrictionName = AttachmentDetails.RestrictionName,
            StatusText = AttachmentDetails.StatusText,
            StatusChanged = AttachmentDetails.StatusChanged,
            DataType = AttachmentDetails.DataType,
            SendersReference = AttachmentDetails.SendersReference,
            StatusHistory = AttachmentStatusMapper.MapToExternal(AttachmentDetails.Statuses),
            CorrespondenceIds = AttachmentDetails.CorrespondenceIds
        };
        return attachment;
    }
}
