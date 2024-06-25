using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.GetAttachmentOverview;

public class GetAttachmentOverviewHandler : IHandler<Guid, GetAttachmentOverviewResponse>
{
    private readonly IAttachmentStatusRepository _attachmentStatusRepository;
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly ICorrespondenceRepository _correspondenceRepository;

    public GetAttachmentOverviewHandler(IAttachmentStatusRepository attachmentStatusRepository, IAttachmentRepository attachmentRepository, ICorrespondenceRepository correspondenceRepository)
    {
        _attachmentStatusRepository = attachmentStatusRepository;
        _attachmentRepository = attachmentRepository;
        _correspondenceRepository = correspondenceRepository;
    }

    public async Task<OneOf<GetAttachmentOverviewResponse, Error>> Process(Guid attachmentId, CancellationToken cancellationToken)
    {
        var attachment = await _attachmentRepository.GetAttachmentById(attachmentId, false, cancellationToken);
        if (attachment == null)
        {
            return Errors.AttachmentNotFound;
        }
        var attachmentStatus = await _attachmentStatusRepository.GetLatestStatusByAttachmentId(attachmentId, cancellationToken);
        var correspondenceIds = await _correspondenceRepository.GetCorrespondenceIdsByAttachmentId(attachmentId, cancellationToken);

        var response = new GetAttachmentOverviewResponse
        {
            AttachmentId = attachment.Id,
            DataLocationUrl = attachment.DataLocationUrl,
            Name = attachment.FileName,
            Status = attachmentStatus.Status,
            StatusText = attachmentStatus.StatusText,
            StatusChanged = attachmentStatus.StatusChanged,
            DataLocationType = attachment.DataLocationType,
            DataType = attachment.DataType,
            SendersReference = attachment.SendersReference,
            CorrespondenceIds = correspondenceIds
        };
        return response;
    }
}
