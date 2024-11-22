using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.GetAttachmentOverview;

public class GetAttachmentOverviewHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    IAttachmentRepository attachmentRepository,
    ICorrespondenceRepository correspondenceRepository,
    UserClaimsHelper userClaimsHelper) : IHandler<Guid, GetAttachmentOverviewResponse>
{
    private readonly IAltinnAuthorizationService _altinnAuthorizationService = altinnAuthorizationService;
    private readonly IAttachmentRepository _attachmentRepository = attachmentRepository;
    private readonly ICorrespondenceRepository _correspondenceRepository = correspondenceRepository;
    private readonly UserClaimsHelper _userClaimsHelper = userClaimsHelper;

    public async Task<OneOf<GetAttachmentOverviewResponse, Error>> Process(Guid attachmentId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var attachment = await _attachmentRepository.GetAttachmentById(attachmentId, true, cancellationToken);
        if (attachment == null)
        {
            return Errors.AttachmentNotFound;
        }
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(user, attachment.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Write }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        if (!_userClaimsHelper.IsSender(attachment.Sender))
        {
            return Errors.InvalidSender;
        }
        var attachmentStatus = attachment.GetLatestStatus();
        var correspondenceIds = await _correspondenceRepository.GetCorrespondenceIdsByAttachmentId(attachmentId, cancellationToken);

        var response = new GetAttachmentOverviewResponse
        {
            AttachmentId = attachment.Id,
            ResourceId = attachment.ResourceId,
            Name = attachment.Name,
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
