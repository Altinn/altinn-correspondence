using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.GetAttachmentDetailsCommand;
using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Mappers;

internal static class AttachmentDetailsMapper
{
    internal static AttachmentDetailsExt MapToExternal(GetAttachmentDetailsCommandResponse AttachmentDetails)
    {
        var attachment = new AttachmentDetailsExt
        {
            AttachmentId = AttachmentDetails.AttachmentId,
            Name = AttachmentDetails.Name ?? string.Empty,
            Status = (AttachmentStatusExt)AttachmentDetails.Status,
            StatusText = AttachmentDetails.StatusText,
            StatusChanged = AttachmentDetails.StatusChanged,
            DataType = AttachmentDetails.DataType,
            IntendedPresentation = (IntendedPresentationTypeExt)AttachmentDetails.IntendedPresentation,
            SendersReference = AttachmentDetails.SendersReference,
            StatusHistory = AttachmentStatusMapper.MapToExternal(AttachmentDetails.Statuses)
        };
        return attachment;
    }
}
