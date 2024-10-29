using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.GetAttachmentOverview;

public class GetAttachmentOverviewHandler : IHandler<Guid, GetAttachmentOverviewResponse>
{
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;
    private readonly IAttachmentStatusRepository _attachmentStatusRepository;
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly ICorrespondenceRepository _correspondenceRepository;

    public GetAttachmentOverviewHandler(IAltinnAuthorizationService altinnAuthorizationService, IAttachmentStatusRepository attachmentStatusRepository, IAttachmentRepository attachmentRepository, ICorrespondenceRepository correspondenceRepository)
    {
        _altinnAuthorizationService = altinnAuthorizationService;
        _attachmentStatusRepository = attachmentStatusRepository;
        _attachmentRepository = attachmentRepository;
        _correspondenceRepository = correspondenceRepository;
    }

    public async Task<OneOf<GetAttachmentOverviewResponse, Error>> Process(Guid attachmentId, CancellationToken cancellationToken)
    {
        var attachment = await _attachmentRepository.GetAttachmentById(attachmentId, true, cancellationToken);
        if (attachment == null)
        {
            return Errors.AttachmentNotFound;
        }
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(attachment.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Write }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        var attachmentStatus = attachment.GetLatestStatus();
        var correspondenceIds = await _correspondenceRepository.GetCorrespondenceIdsByAttachmentId(attachmentId, cancellationToken);

        var response = new GetAttachmentOverviewResponse
        {
            AttachmentId = attachment.Id,
            ResourceId = attachment.ResourceId,
            Name = attachment.FileName,
            Checksum = attachment.Checksum,
            Status = attachmentStatus.Status,
            StatusText = attachmentStatus.StatusText,
            StatusChanged = attachmentStatus.StatusChanged,
            DataLocationType = attachment.DataLocationType,
            DataType = attachment.DataType,
            SendersReference = attachment.SendersReference,
            CorrespondenceIds = correspondenceIds,
            FileName = attachment.FileName,
            Sender = attachment.Sender,
        };
        return response;
    }
}
