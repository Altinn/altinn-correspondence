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
    private readonly GetCorrespondenceHelper _getCorrespondenceHelper;

    public GetCorrespondenceDetailsHandler(IAltinnAuthorizationService altinnAuthorizationService, ICorrespondenceRepository correspondenceRepository, ICorrespondenceStatusRepository correspondenceStatusRepository, GetCorrespondenceHelper getCorrespondenceHelper)
    {
        _altinnAuthorizationService = altinnAuthorizationService;
        _correspondenceRepository = correspondenceRepository;
        _correspondenceStatusRepository = correspondenceStatusRepository;
        _getCorrespondenceHelper = getCorrespondenceHelper;
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
        var latestStatus = correspondence.Statuses?.OrderByDescending(s => s.StatusChanged).FirstOrDefault();
        if (latestStatus == null)
        {
            return Errors.CorrespondenceNotFound;
        }

        var userOrgNo = _getCorrespondenceHelper.GetUserID();
        bool isRecipient = correspondence.Recipient == userOrgNo;

        if (isRecipient && latestStatus.Status == CorrespondenceStatus.Published)
        {
            latestStatus = new CorrespondenceStatusEntity{
                CorrespondenceId = correspondence.Id,
                Status = CorrespondenceStatus.Fetched,
                StatusText = CorrespondenceStatus.Fetched.ToString(),
                StatusChanged = DateTimeOffset.Now
            };
        
            await _correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity
            {
                CorrespondenceId = correspondence.Id,
                Status = latestStatus.Status,
                StatusText = latestStatus.StatusText,
                StatusChanged = latestStatus.StatusChanged
            }, cancellationToken);
        }

        var response = new GetCorrespondenceDetailsResponse
        {
            CorrespondenceId = correspondence.Id,
            Status = latestStatus.Status,
            StatusText = latestStatus.StatusText,
            StatusChanged = latestStatus.StatusChanged,
            SendersReference = correspondence.SendersReference,
            Created = correspondence.Created,
            Recipient = correspondence.Recipient,
            Content = correspondence.Content!,
            ReplyOptions = correspondence.ReplyOptions == null ? new List<CorrespondenceReplyOptionEntity>() : correspondence.ReplyOptions,
            Notifications = correspondence.Notifications == null ? new List<CorrespondenceNotificationEntity>() : correspondence.Notifications,
            VisibleFrom = correspondence.VisibleFrom,
            IsReservable = correspondence.IsReservable == null || correspondence.IsReservable.Value,
            StatusHistory = correspondence.Statuses,
            ResourceId = correspondence.ResourceId
        };
        return response;
    }
}