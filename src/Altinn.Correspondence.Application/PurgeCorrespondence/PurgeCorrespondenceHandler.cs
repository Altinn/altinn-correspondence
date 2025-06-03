using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
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
        logger.LogInformation("Processing purge request for correspondence {CorrespondenceId}", correspondenceId);
        logger.LogDebug("Retrieving correspondence {CorrespondenceId} with status history", correspondenceId);
        var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, true, false, false, cancellationToken);
        if (correspondence == null)
        {
            logger.LogWarning("Correspondence {CorrespondenceId} not found", correspondenceId);
            return CorrespondenceErrors.CorrespondenceNotFound;
        }

        logger.LogDebug("Checking sender access for correspondence {CorrespondenceId}", correspondenceId);
        var hasAccessAsSender = await altinnAuthorizationService.CheckAccessAsSender(
            user,
            correspondence,
            cancellationToken);
        logger.LogDebug("Checking recipient access for correspondence {CorrespondenceId}", correspondenceId);
        var hasAccessAsRecipient = await altinnAuthorizationService.CheckAccessAsRecipient(
            user,
            correspondence,
            cancellationToken);

        if (user is null)
        {
            logger.LogError("Purge operation attempted without authenticated user context");
            throw new InvalidOperationException("This operation cannot be called outside an authenticated HttpContext");
        }

        logger.LogDebug("Validating user permissions for correspondence {CorrespondenceId}", correspondenceId);
        var authError = CheckUserPermissions(user, correspondence, hasAccessAsSender, hasAccessAsRecipient, out bool isSender);
        if (authError is not null)
        {
            logger.LogWarning("Permission validation failed for correspondence {CorrespondenceId}: {Error}", 
                correspondenceId, 
                authError);
            return authError;
        }

        logger.LogDebug("Getting caller organization ID for correspondence {CorrespondenceId}", correspondenceId);
        var callerId = user.GetCallerOrganizationId();
        if (callerId is null)
        {
            logger.LogError("Could not determine caller organization ID for correspondence {CorrespondenceId}", correspondenceId);
            return AuthorizationErrors.CouldNotDetermineCaller;
        }

        logger.LogDebug("Looking up party information for organization {OrganizationId}", callerId);
        var party = await altinnRegisterService.LookUpPartyById(callerId, cancellationToken);
        if (party?.PartyUuid is not Guid partyUuid)
        {
            logger.LogError("Could not find party UUID for organization {OrganizationId}", callerId);
            return AuthorizationErrors.CouldNotFindPartyUuid;
        }

        logger.LogDebug("Retrieved party UUID {PartyUuid} for organization {OrganizationId}", partyUuid, callerId);

        logger.LogInformation("Starting purge process for correspondence {CorrespondenceId} as {Role}", 
            correspondenceId, 
            isSender ? "sender" : "recipient");

        return await TransactionWithRetriesPolicy.Execute<Guid>(async (cancellationToken) =>
        {
            logger.LogDebug("Executing purge operation for correspondence {CorrespondenceId}", correspondenceId);
            var result = await purgeCorrespondenceHelper.PurgeCorrespondence(correspondence, isSender, partyUuid, cancellationToken);
            logger.LogInformation("Successfully purged correspondence {CorrespondenceId}", correspondenceId);
            return result;
        }, logger, cancellationToken);
    }

    private Error? CheckUserPermissions(ClaimsPrincipal user, CorrespondenceEntity correspondence, bool hasAccessAsSender, bool hasAccessAsRecipient, out bool isSender)
    {
        isSender = false;
        if (!hasAccessAsSender && !hasAccessAsRecipient)
        {
            logger.LogWarning("User has neither sender nor recipient access to correspondence {CorrespondenceId}", correspondence.Id);
            return AuthorizationErrors.NoAccessToResource;
        }
        else if ((hasAccessAsSender && user.CallingAsSender()) || (!hasAccessAsRecipient && hasAccessAsSender))
        {
            isSender = true;
            logger.LogDebug("Validating purge request as sender for correspondence {CorrespondenceId}", correspondence.Id);
            var senderError = purgeCorrespondenceHelper.ValidatePurgeRequestSender(correspondence);
            if (senderError is not null)
            {
                logger.LogWarning("Sender validation failed for correspondence {CorrespondenceId}: {Error}", 
                    correspondence.Id, 
                    senderError);
                return senderError;
            }
        }
        else if ((hasAccessAsRecipient && user.CallingAsRecipient()) || (!hasAccessAsSender && hasAccessAsRecipient))
        {
            logger.LogDebug("Validating purge request as recipient for correspondence {CorrespondenceId}", correspondence.Id);
            var recipientError = purgeCorrespondenceHelper.ValidatePurgeRequestRecipient(correspondence);
            if (recipientError is not null)
            {
                logger.LogWarning("Recipient validation failed for correspondence {CorrespondenceId}: {Error}", 
                    correspondence.Id, 
                    recipientError);
                return recipientError;
            }
        }
        else
        {
            // User has delegated permissions to both sender and recipient
            logger.LogDebug("User has both sender and recipient access, trying sender first for correspondence {CorrespondenceId}", correspondence.Id);
            // Try as sender first
            var senderError = purgeCorrespondenceHelper.ValidatePurgeRequestSender(correspondence);
            if (senderError is null)
            {
                isSender = true;
                logger.LogDebug("Successfully validated purge request as sender for correspondence {CorrespondenceId}", correspondence.Id);
                return null;
            }
            // If sender fails, try as recipient
            logger.LogDebug("Sender validation failed, trying recipient for correspondence {CorrespondenceId}", correspondence.Id);
            var recipientError = purgeCorrespondenceHelper.ValidatePurgeRequestRecipient(correspondence);
            if (recipientError is not null)
            {
                logger.LogWarning("Both sender and recipient validation failed for correspondence {CorrespondenceId}", correspondence.Id);
                return senderError;
            }
        }
        logger.LogDebug("Successfully validated permissions for correspondence {CorrespondenceId}", correspondence.Id);
        return null;
    }
}