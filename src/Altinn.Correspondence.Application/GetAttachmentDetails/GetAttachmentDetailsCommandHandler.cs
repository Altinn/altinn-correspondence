using Altinn.Correspondence.Application.GetAttachmentDetailsCommand;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.EntityFrameworkCore.Query;
using OneOf;

namespace Altinn.Correspondence.Application.GetAttachmentDetailsCommand;

public class GetAttachmentDetailsCommandHandler : IHandler<Guid, GetAttachmentDetailsCommandResponse>
{
    private readonly IAttachmentStatusRepository _attachmentStatusRepository;
    private readonly IAttachmentRepository _attachmentRepository;
    public GetAttachmentDetailsCommandHandler(IAttachmentStatusRepository attachmentStatusRepository, IAttachmentRepository attachmentRepository)
    {
        _attachmentStatusRepository = attachmentStatusRepository;
        _attachmentRepository = attachmentRepository;
    }

    public async Task<OneOf<GetAttachmentDetailsCommandResponse, Error>> Process(Guid attachmentId, CancellationToken cancellationToken)
    {
        var attachment = await _attachmentRepository.GetAttachmentById(attachmentId, true, cancellationToken);
        if (attachment == null)
        {
            return Errors.AttachmentNotFound;
        }
        var attachmentStatus = attachment.Statuses.OrderByDescending(s => s.StatusChanged).First();

        var response = new GetAttachmentDetailsCommandResponse
        {
            AttachmentId = attachment.Id,
            DataLocationUrl = attachment.DataLocationUrl,
            Name = attachment.FileName,
            Status = attachmentStatus.Status,
            Statuses = attachment.Statuses,
            StatusChanged = attachmentStatus.StatusChanged,
            DataLocationType = attachment.DataLocationType,
            DataType = attachment.DataType,
            IntendedPresentation = attachment.IntendedPresentation,
            SendersReference = attachment.SendersReference
        };
        return response;
    }


}
