using Altinn.Correspondence.Application.Helpers;
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
public class LegacyPurgeCorrespondenceHandler(
    ICorrespondenceRepository correspondenceRepository,
    IAltinnAuthorizationService altinnAuthorizationService,
    IAltinnRegisterService altinnRegisterService,
    PurgeCorrespondenceHelper purgeCorrespondenceHelper,
    UserClaimsHelper userClaimsHelper,
    ILogger<LegacyPurgeCorrespondenceHandler> logger) : IHandler<Guid, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(Guid correspondenceId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var operationTimestamp = DateTimeOffset.UtcNow;
        if (userClaimsHelper.GetPartyId() is not int partyId)
        {
            return AuthorizationErrors.InvalidPartyId;
        }
        var party = await altinnRegisterService.LookUpPartyByPartyId(partyId, cancellationToken);
        if (party is null || (string.IsNullOrEmpty(party.SSN) && string.IsNullOrEmpty(party.OrgNumber)))
        {
            return AuthorizationErrors.CouldNotFindOrgNo;
        }
        var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, true, false, false, cancellationToken, true);
        if (correspondence == null)
        {
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        var minimumAuthLevel = await altinnAuthorizationService.CheckUserAccessAndGetMinimumAuthLevel(user, party.SSN, correspondence.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Read }, correspondence.Recipient, cancellationToken);
        if (minimumAuthLevel == null)
        {
            return AuthorizationErrors.LegacyNoAccessToCorrespondence;
        }
        var recipientPurgeError = purgeCorrespondenceHelper.ValidatePurgeRequestRecipient(correspondence, true);
        if (recipientPurgeError is not null)
        {
            return recipientPurgeError;
        }
        if (party.PartyUuid is not Guid partyUuid)
        {
            return AuthorizationErrors.CouldNotFindPartyUuid;
        }
        return await TransactionWithRetriesPolicy.Execute<Guid>(async (cancellationToken) =>
        {
            return await purgeCorrespondenceHelper.PurgeCorrespondence(correspondence, false, partyUuid, operationTimestamp, cancellationToken);
        }, logger, cancellationToken);
    }
}