using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.UpdateCorrespondenceStatus;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.SyncCorrespondenceEvent;

public class SyncCorrespondenceNotificationEventHandler(
    ICorrespondenceNotificationRepository notificationRepository,    
    ILogger<SyncCorrespondenceNotificationEventHandler> logger) : IHandler<SyncCorrespondenceNotificationEventRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(SyncCorrespondenceNotificationEventRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

}
