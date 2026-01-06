using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Hangfire;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;
using Altinn.Correspondence.Application.VerifyCorrespondenceConfirmation;

namespace Altinn.Correspondence.Application.ConfirmCorrespondence;

public class ConfirmCorrespondenceHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    IAltinnRegisterService altinnRegisterService,
    ICorrespondenceRepository correspondenceRepository,
    IBackgroundJobClient backgroundJobClient,
    IDialogportenService dialogportenService,
    ILogger<ConfirmCorrespondenceHandler> logger) : IHandler<ConfirmCorrespondenceRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(ConfirmCorrespondenceRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing confirmation request for correspondence {CorrespondenceId}", 
            request.CorrespondenceId);
        var operationTimestamp = DateTimeOffset.UtcNow;
        
        var correspondence = await correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, true, false, false, cancellationToken);
        if (correspondence == null)
        {
            logger.LogWarning("Correspondence {CorrespondenceId} not found", request.CorrespondenceId);
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        
        var hasAccess = await altinnAuthorizationService.CheckAccessAsRecipient(
            user,
            correspondence,
            cancellationToken);
        if (!hasAccess)
        {
            logger.LogWarning("Access denied for correspondence {CorrespondenceId} - user does not have recipient access", request.CorrespondenceId);
            return AuthorizationErrors.NoAccessToResource;
        }

        var currentStatusError = ValidateCurrentStatus(correspondence);
        if (currentStatusError is not null)
        {
            logger.LogWarning("Current status validation failed for correspondence {CorrespondenceId}: {Error}", 
                request.CorrespondenceId, 
                currentStatusError);
            return currentStatusError;
        }
        
        var updateError = ValidateConfirmRequest(correspondence);
        if (updateError is not null)
        {
            logger.LogWarning("Confirm request validation failed for correspondence {CorrespondenceId}: {Error}", 
                request.CorrespondenceId, 
                updateError);
            return updateError;
        }

        var caller = user?.GetCallerPartyUrn();
        if (string.IsNullOrWhiteSpace(caller))
        {
            return AuthorizationErrors.CouldNotDetermineCaller;
        }

        var party = await altinnRegisterService.LookUpPartyById(caller, cancellationToken);
        if (party?.PartyUuid is not Guid partyUuid)
        {
            logger.LogError("Could not find party UUID for caller {caller}", caller);
            return AuthorizationErrors.CouldNotFindPartyUuid;
        }

        var verifyJobId = backgroundJobClient.Schedule<VerifyCorrespondenceConfirmationHandler>(
            handler => handler.VerifyPatchAndCommitConfirmation(correspondence.Id, partyUuid, party.PartyId, operationTimestamp, caller, CancellationToken.None),
            TimeSpan.FromSeconds(4));
        
        try
        {
            using var patchCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            patchCancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(2));
            await dialogportenService.PatchCorrespondenceDialogToConfirmed(correspondence.Id, patchCancellationTokenSource.Token);
        }
        catch (Exception)
        {
            var deleted = backgroundJobClient.Delete(verifyJobId);
            if (!deleted)
            {
                logger.LogWarning("Failed to delete verify job {VerifyJobId} after patch failure for correspondence {CorrespondenceId}. Job may retry until attempts are exhausted.",
                    verifyJobId,
                    correspondence.Id);
            }
            throw;
        }
        
        logger.LogInformation("Successfully confirmed correspondence {CorrespondenceId}", 
            request.CorrespondenceId);
        return request.CorrespondenceId;
    }

    private Error? ValidateCurrentStatus(CorrespondenceEntity correspondence)
    {
        var currentStatus = correspondence.GetHighestStatus();
        if (currentStatus is null)
        {
            return CorrespondenceErrors.CouldNotRetrieveStatus;
        }
        if (!currentStatus.Status.IsAvailableForRecipient())
        {
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        if (currentStatus!.Status.IsPurged())
        {
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        return null;
    }

    private Error? ValidateConfirmRequest(CorrespondenceEntity correspondence)
    {
        if (!correspondence.StatusHasBeen(CorrespondenceStatus.Fetched))
        {
            return CorrespondenceErrors.ConfirmBeforeFetched;
        }
        if (correspondence.StatusHasBeen(CorrespondenceStatus.Confirmed))
        {
            return CorrespondenceErrors.CorrespondenceAlreadyConfirmed;
        }
        return null;
    }
} 