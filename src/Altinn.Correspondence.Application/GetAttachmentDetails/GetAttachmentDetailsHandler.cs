using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.GetAttachmentDetails;

public class GetAttachmentDetailsHandler : IHandler<Guid, GetAttachmentDetailsResponse>
{
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;

    public GetAttachmentDetailsHandler(IAttachmentRepository attachmentRepository, ICorrespondenceRepository correspondenceRepository, IAltinnAuthorizationService altinnAuthorizationService)
    {
        _attachmentRepository = attachmentRepository;
        _correspondenceRepository = correspondenceRepository;
        _altinnAuthorizationService = altinnAuthorizationService;
    }

    public async Task<OneOf<GetAttachmentDetailsResponse, Error>> Process(Guid attachmentId, CancellationToken cancellationToken)
    {
        var attachment = await _attachmentRepository.GetAttachmentById(attachmentId, true, cancellationToken);
        if (attachment == null)
        {
            return Errors.AttachmentNotFound;
        }
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(attachment.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Open }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        var correspondenceIds = await _correspondenceRepository.GetCorrespondenceIdsByAttachmentId(attachmentId, cancellationToken);
        var attachmentStatus = attachment.Statuses.OrderByDescending(s => s.StatusChanged).First();

        var response = new GetAttachmentDetailsResponse
        {
            ResourceId = attachment.ResourceId,
            AttachmentId = attachment.Id,
            DataLocationUrl = attachment.DataLocationUrl,
            Name = attachment.FileName,
            Status = attachmentStatus.Status,
            Statuses = attachment.Statuses,
            StatusChanged = attachmentStatus.StatusChanged,
            DataLocationType = attachment.DataLocationType,
            DataType = attachment.DataType,
            SendersReference = attachment.SendersReference,
            CorrespondenceIds = correspondenceIds,
        };
        return response;
    }
}
