using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.GetAttachmentDetails;

public class GetAttachmentDetailsHandler : IHandler<Guid, GetAttachmentDetailsResponse>
{
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;
    private readonly UserClaimsHelper _userClaimsHelper;

    public GetAttachmentDetailsHandler(IAttachmentRepository attachmentRepository, ICorrespondenceRepository correspondenceRepository, IAltinnAuthorizationService altinnAuthorizationService, UserClaimsHelper userClaimsHelper)
    {
        _attachmentRepository = attachmentRepository;
        _correspondenceRepository = correspondenceRepository;
        _altinnAuthorizationService = altinnAuthorizationService;
        _userClaimsHelper = userClaimsHelper;
    }

    public async Task<OneOf<GetAttachmentDetailsResponse, Error>> Process(Guid attachmentId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var attachment = await _attachmentRepository.GetAttachmentById(attachmentId, true, cancellationToken);
        if (attachment == null)
        {
            return Errors.AttachmentNotFound;
        }
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(user, attachment.ResourceId, attachment.Sender, attachment.Id.ToString(), new List<ResourceAccessLevel> { ResourceAccessLevel.Write }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        if (!_userClaimsHelper.IsSender(attachment.Sender))
        {
            return Errors.InvalidSender;
        }
        var correspondenceIds = await _correspondenceRepository.GetCorrespondenceIdsByAttachmentId(attachmentId, cancellationToken);
        var attachmentStatus = attachment.GetLatestStatus();

        var response = new GetAttachmentDetailsResponse
        {
            ResourceId = attachment.ResourceId,
            AttachmentId = attachment.Id,
            Name = attachment.Name,
            Status = attachmentStatus.Status,
            Statuses = attachment.Statuses,
            StatusText = attachmentStatus.StatusText,
            StatusChanged = attachmentStatus.StatusChanged,
            DataLocationType = attachment.DataLocationType,
            DataType = attachment.DataType,
            SendersReference = attachment.SendersReference,
            CorrespondenceIds = correspondenceIds,
            FileName = attachment.FileName,
            Sender = attachment.Sender,
            IsEncrypted = attachment.IsEncrypted,
            Checksum = attachment.Checksum,
        };
        return response;
    }
}
