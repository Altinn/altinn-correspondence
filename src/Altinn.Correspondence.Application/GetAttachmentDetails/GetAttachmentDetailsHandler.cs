using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.GetAttachmentDetails;

public class GetAttachmentDetailsHandler : IHandler<Guid, GetAttachmentDetailsResponse>
{
    private readonly IAttachmentStatusRepository _attachmentStatusRepository;
    private readonly IAttachmentRepository _attachmentRepository;
    public GetAttachmentDetailsHandler(IAttachmentStatusRepository attachmentStatusRepository, IAttachmentRepository attachmentRepository)
    {
        _attachmentStatusRepository = attachmentStatusRepository;
        _attachmentRepository = attachmentRepository;
    }

    public async Task<OneOf<GetAttachmentDetailsResponse, Error>> Process(Guid attachmentId, CancellationToken cancellationToken)
    {
        var attachment = await _attachmentRepository.GetAttachmentById(attachmentId, true, cancellationToken);
        if (attachment == null)
        {
            return Errors.AttachmentNotFound;
        }
        var attachmentStatus = attachment.Statuses.OrderByDescending(s => s.StatusChanged).First();

        var response = new GetAttachmentDetailsResponse
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
