using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.GetCorrespondenceDetails;

public class GetCorrespondenceDetailsHandler : IHandler<Guid, GetCorrespondenceDetailsResponse>
{
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly ICorrespondenceStatusRepository _correspondenceStatusRepository;
    private readonly UserClaimsHelper _userClaimsHelper;

    public GetCorrespondenceDetailsHandler(IAltinnAuthorizationService altinnAuthorizationService, ICorrespondenceRepository correspondenceRepository, ICorrespondenceStatusRepository correspondenceStatusRepository, UserClaimsHelper userClaimsHelper)
    {
        _altinnAuthorizationService = altinnAuthorizationService;
        _correspondenceRepository = correspondenceRepository;
        _correspondenceStatusRepository = correspondenceStatusRepository;
        _userClaimsHelper = userClaimsHelper;
    }

    public async Task<OneOf<GetCorrespondenceDetailsResponse, Error>> Process(Guid correspondenceId, CancellationToken cancellationToken)
    {
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, cancellationToken);
        if (correspondence == null)
        {
            return Errors.CorrespondenceNotFound;
        }
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(correspondence.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.See }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        var latestStatus = correspondence.Statuses?
            .OrderByDescending(s => s.StatusChanged)
            .SkipWhile(s => s.Status == CorrespondenceStatus.Fetched)
            .FirstOrDefault();
        if (latestStatus == null)
        {
            return Errors.CorrespondenceNotFound;
        }

        var userOrgNo = _userClaimsHelper.GetUserID();
        bool isRecipient = correspondence.Recipient == userOrgNo;

        if (isRecipient && latestStatus.Status >= CorrespondenceStatus.Published)
        {
            await _correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity
            {
                CorrespondenceId = correspondence.Id,
                Status = CorrespondenceStatus.Fetched,
                StatusText = CorrespondenceStatus.Fetched.ToString(),
                StatusChanged = DateTime.Now
            }, cancellationToken);
        }

        var response = new GetCorrespondenceDetailsResponse
        {
            CorrespondenceId = correspondence.Id,
            Status = latestStatus.Status,
            StatusText = latestStatus.StatusText,
            StatusChanged = latestStatus.StatusChanged,
            SendersReference = correspondence.SendersReference,
            Sender = correspondence.Sender,
            MessageSender = correspondence.MessageSender ?? string.Empty,
            Created = correspondence.Created,
            Recipient = correspondence.Recipient,
            Content = correspondence.Content!,
            ReplyOptions = correspondence.ReplyOptions ?? new List<CorrespondenceReplyOptionEntity>(),
            Notifications = correspondence.Notifications ?? new List<CorrespondenceNotificationEntity>(),
            StatusHistory = correspondence.Statuses?.OrderBy(s => s.StatusChanged).ToList() ?? new List<CorrespondenceStatusEntity>(),
            ExternalReferences = correspondence.ExternalReferences ?? new List<ExternalReferenceEntity>(),
            ResourceId = correspondence.ResourceId,
            VisibleFrom = correspondence.VisibleFrom,
            IsReservable = correspondence.IsReservable == null || correspondence.IsReservable.Value,
            MarkedUnread = correspondence.MarkedUnread,
            AllowSystemDeleteAfter = correspondence.AllowSystemDeleteAfter,
            DueDateTime = correspondence.DueDateTime,
            PropertyList = correspondence.PropertyList,
        };
        return response;
    }
}
