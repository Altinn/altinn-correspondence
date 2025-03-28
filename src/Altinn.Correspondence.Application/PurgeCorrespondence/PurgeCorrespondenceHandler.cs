using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.PurgeCorrespondence;

public class PurgeCorrespondenceHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    IAltinnRegisterService altinnRegisterService,
    ICorrespondenceRepository correspondenceRepository,
    PurgeCorrespondenceHelper purgeCorrespondenceHelper,
    ILogger<PurgeCorrespondenceHandler> logger) : IHandler<PurgeCorrespondenceRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(PurgeCorrespondenceRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        Guid correspondenceId = request.CorrespondenceId;
        var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, true, false, false, cancellationToken);
        if (correspondence == null)
        {
            return CorrespondenceErrors.CorrespondenceNotFound;
        }

        var hasAccessAsSender = await altinnAuthorizationService.CheckAccessAsSender(
            user,
            correspondence,
            cancellationToken);
        var hasAccessAsRecipient = await altinnAuthorizationService.CheckAccessAsRecipient(
            user,
            correspondence,
            cancellationToken);

        if (user is null)
        {
            throw new InvalidOperationException("This operation cannot be called outside an authenticated HttpContext");
        }
        var authError = CheckUserPermissions(user, correspondence, hasAccessAsSender, hasAccessAsRecipient, out bool isSender);
        if (authError is not null)
        {
            return authError;
        }
        var callerId = user.GetCallerOrganizationId();
        if (callerId is null)
        {
            return AuthorizationErrors.CouldNotDetermineCaller;
        }
        var party = await altinnRegisterService.LookUpPartyById(callerId, cancellationToken);
        if (party?.PartyUuid is not Guid partyUuid)
        {
            return AuthorizationErrors.CouldNotFindPartyUuid;
        }

        return await TransactionWithRetriesPolicy.Execute<Guid>(async (cancellationToken) =>
        {
            return await purgeCorrespondenceHelper.PurgeCorrespondence(correspondence, isSender, partyUuid, cancellationToken);
        }, logger, cancellationToken);
    }
    private Error? CheckUserPermissions(ClaimsPrincipal user, CorrespondenceEntity correspondence, bool hasAccessAsSender, bool hasAccessAsRecipient, out bool isSender)
    {
        isSender = false;
        if (!hasAccessAsSender && !hasAccessAsRecipient)
        {
            return AuthorizationErrors.NoAccessToResource;
        }
        else if ((hasAccessAsSender && user.CallingAsSender()) || (!hasAccessAsRecipient && hasAccessAsSender))
        {
            isSender = true;
            var senderError = purgeCorrespondenceHelper.ValidatePurgeRequestSender(correspondence);
            if (senderError is not null)
            {
                return senderError;
            }
        }
        else if ((hasAccessAsRecipient && user.CallingAsRecipient()) || (!hasAccessAsSender && hasAccessAsRecipient))
        {
            var recipientError = purgeCorrespondenceHelper.ValidatePurgeRequestRecipient(correspondence);
            if (recipientError is not null)
            {
                return recipientError;
            }
        }
        else
        {// User has delegated permissions to both sender and recipient
            // Try as sender first
            var senderError = purgeCorrespondenceHelper.ValidatePurgeRequestSender(correspondence);
            if (senderError is null)
            {
                isSender = true;
                return null;
            }
            // If sender fails, try as recipient
            var recipientError = purgeCorrespondenceHelper.ValidatePurgeRequestRecipient(correspondence);
            if (recipientError is not null)
            {
                return senderError;
            }
        }
        return null;
    }
}