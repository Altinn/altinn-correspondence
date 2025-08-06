using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.UpdateCorrespondenceStatus;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.SyncCorrespondenceStatusEvent;

public class SyncCorrespondenceForwardingEventHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    IAltinnRegisterService altinnRegisterService,
    ICorrespondenceRepository correspondenceRepository,    
    ILogger<SyncCorrespondenceForwardingEventHandler> logger) : IHandler<SyncCorrespondenceForwardingEventRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(SyncCorrespondenceForwardingEventRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

}
