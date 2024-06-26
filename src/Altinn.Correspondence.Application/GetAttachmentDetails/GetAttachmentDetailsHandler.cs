using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.GetAttachmentDetails;

public class GetAttachmentDetailsHandler : IHandler<Guid, GetAttachmentDetailsResponse>
{
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly ICorrespondenceRepository _correspondenceRepository;

    public GetAttachmentDetailsHandler(IAttachmentRepository attachmentRepository, ICorrespondenceRepository correspondenceRepository)
    {
        _attachmentRepository = attachmentRepository;
        _correspondenceRepository = correspondenceRepository;
    }

    public async Task<OneOf<GetAttachmentDetailsResponse, Error>> Process(Guid attachmentId, CancellationToken cancellationToken)
    {
        var attachment = await _attachmentRepository.GetAttachmentById(attachmentId, true, cancellationToken);
        if (attachment == null)
        {
            return Errors.AttachmentNotFound;
        }
        var correspondenceIds = await _correspondenceRepository.GetCorrespondenceIdsByAttachmentId(attachmentId, cancellationToken);
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
            SendersReference = attachment.SendersReference,
            CorrespondenceIds = correspondenceIds
        };
        return response;
    }
}
