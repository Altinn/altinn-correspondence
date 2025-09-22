using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.GetCorrespondences;

public class LegacyGetCorrespondencesHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    IAltinnAccessManagementService altinnAccessManagementService,
    ICorrespondenceRepository correspondenceRepository,
    UserClaimsHelper userClaimsHelper,
    IAltinnRegisterService altinnRegisterService,
    IResourceRegistryService resourceRegistryService,
    ILogger<LegacyGetCorrespondencesHandler> logger) : IHandler<LegacyGetCorrespondencesRequest, LegacyGetCorrespondencesResponse>
{
    private record PartyInfo(string Id, Party? Party);

    public async Task<OneOf<LegacyGetCorrespondencesResponse, Error>> Process(LegacyGetCorrespondencesRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        const int limit = 1000;
        DateTimeOffset? to = request.To != null ? ((DateTimeOffset)request.To).ToUniversalTime() : null;
        DateTimeOffset? from = request.From != null ? ((DateTimeOffset)request.From).ToUniversalTime() : null;

        if (from != null && to != null && from > to)
        {
            return CorrespondenceErrors.InvalidDateRange;
        }
        if (userClaimsHelper.GetPartyId() is not int partyId)
        {
            return AuthorizationErrors.InvalidPartyId;
        }
        logger.LogInformation("Searching legacy for party {partyId} with parameters: From={from}, To={to}, Status={status}, IncludeActive={includeActive}, IncludeArchived={includeArchived}, IncludeDeleted={includeDeleted}, FilterMigrated={filterMigrated}, SearchString={searchString}, InstanceOwnerPartyIds={instanceOwnerPartyIds}", 
            partyId, 
            from?.ToString("yyyy-MM-dd HH:mm:ss"), 
            to?.ToString("yyyy-MM-dd HH:mm:ss"), 
            request.Status, 
            request.IncludeActive, 
            request.IncludeArchived, 
            request.IncludeDeleted, 
            request.FilterMigrated, 
            request.SearchString?.SanitizeForLogging(), 
            request.InstanceOwnerPartyIdList);
        var minAuthLevel = userClaimsHelper.GetMinimumAuthenticationLevel();
        var userParty = await altinnRegisterService.LookUpPartyByPartyId(partyId, cancellationToken);
        if (userParty == null || (string.IsNullOrEmpty(userParty.SSN) && string.IsNullOrEmpty(userParty.OrgNumber)))
        {
            logger.LogWarning("Unauthorized because could not find org number in Altinn Register.");
            return AuthorizationErrors.CouldNotFindOrgNo;
        }
        var recipients = new List<string>();
        if (request.InstanceOwnerPartyIdList != null && request.InstanceOwnerPartyIdList.Length > 0)
        {
            var authorizedPartiesResponse = await altinnAccessManagementService.GetAuthorizedParties(userParty, cancellationToken);
            var authorizedParties = new List<PartyWithSubUnits>();
            foreach(var authorizedParty in authorizedPartiesResponse)
            {
                if (!authorizedParty.OnlyHierarchyElementWithNoAccess)
                {
                    authorizedParties.Add(authorizedParty);
                } 
                
                authorizedParties.AddRange(authorizedParty.SubUnits);
            }
            authorizedParties = authorizedParties.DistinctBy(party => party.PartyId).ToList();
            var authorizedPartiesDict = authorizedParties.ToDictionary(p => p.PartyId, p => p);
            foreach (int instanceOwnerPartyId in request.InstanceOwnerPartyIdList)
            {
                if (!authorizedPartiesDict.TryGetValue(instanceOwnerPartyId, out var mappedInstanceOwner))
                {
                    logger.LogWarning("Unauthorized because not an authorized party");
                    return AuthorizationErrors.LegacyNotAccessToOwner(instanceOwnerPartyId);
                }
                if (mappedInstanceOwner.OrgNumber != null)
                    recipients.Add(GetPrefixedForOrg(mappedInstanceOwner.OrgNumber));
                else if (mappedInstanceOwner.SSN != null)
                    recipients.Add(GetPrefixedForPerson(mappedInstanceOwner.SSN));
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(userParty.SSN)) recipients.Add(GetPrefixedForPerson(userParty.SSN));
            if (!string.IsNullOrEmpty(userParty.OrgNumber)) recipients.Add(GetPrefixedForOrg(userParty.OrgNumber));
        }
        List<string> resourcesToSearch = new List<string>();

        // Get all correspondences owned by Recipients
        // request.IncludeDeleted is not used as this is for soft deleted correspondences only, which are not relevant in legacy
        var correspondences = await correspondenceRepository.GetCorrespondencesForParties(limit: limit,
                                                                                          from: from,
                                                                                          to: to,
                                                                                          status: request.Status,
                                                                                          recipientIds: recipients,
                                                                                          resourceIds: resourcesToSearch,
                                                                                          includeActive: request.IncludeActive,
                                                                                          includeArchived: request.IncludeArchived,
                                                                                          searchString: request.SearchString,
                                                                                          cancellationToken: cancellationToken,
                                                                                          filterMigrated: request.FilterMigrated);
        
        var resourceIds = correspondences.Select(c => c.ResourceId).Distinct().ToList();
        logger.LogInformation("Retrieved {correspondenceCount} from database of {distinctResourceIdCount} different resources", correspondences.Count, resourceIds.Count);
        var authorizedCorrespondences = new List<CorrespondenceEntity>();
        List<LegacyCorrespondenceItem> correspondenceItems = new List<LegacyCorrespondenceItem>();

        var Senders = new List<PartyInfo>();
        foreach (var orgNr in correspondences.Select(c => c.Sender).Distinct().ToList())
        {
            try
            {
                var resourceOwnerParty = await altinnRegisterService.LookUpPartyById(orgNr, cancellationToken);
                Senders.Add(new PartyInfo(orgNr, resourceOwnerParty));
            }
            catch (Exception e)
            {
                Senders.Add(new PartyInfo(orgNr, null));
            }
        }

        var resourceOwners = new Dictionary<string, string>();
        foreach (var resource in correspondences.Select(c => c.ResourceId).Distinct().ToList())
        {
            var resourceOwner = await resourceRegistryService.GetServiceOwnerNameOfResource(resource, cancellationToken);
            if (resourceOwner == null)
            {
                logger.LogError("Failed to get resource owner for resource {Resource}", resource);
                resourceOwners.Add(resource, "");
            }
            else
            {
                resourceOwners.Add(resource, resourceOwner);
            }
        }

        var recipientDetails = new List<PartyInfo>();
        foreach (var orgNr in correspondences.Select(c => c.Recipient).Distinct().ToList())
        {
            try
            {
                var recipientParty = await altinnRegisterService.LookUpPartyById(orgNr, cancellationToken);
                recipientDetails.Add(new PartyInfo(orgNr, recipientParty));
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to lookup recipient party for orgNr: {OrgNr}", orgNr);
                recipientDetails.Add(new PartyInfo(orgNr, null));
            }
        }

        Dictionary<string, int?> authlevels = new(correspondences.Count);
        foreach (var correspondence in correspondences)
        {
            string authLevelKey = $"{correspondence.Recipient}::{correspondence.ResourceId}";
            if (!authlevels.TryGetValue(authLevelKey, out int? authLevel))
            {
                authLevel = await altinnAuthorizationService.CheckUserAccessAndGetMinimumAuthLevel(user, userParty.SSN, correspondence.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Read }, correspondence.Recipient, cancellationToken);
                authlevels.Add(authLevelKey, authLevel);
            }
            if (authLevel == null || minAuthLevel < authLevel)
            {
                continue;
            }
            var purgedStatus = correspondence.GetPurgedStatus();
            var sender = Senders.SingleOrDefault(r => r.Id == correspondence.Sender)?.Party;
            var recipient = recipientDetails.SingleOrDefault(r => r.Id == correspondence.Recipient)?.Party;
            correspondenceItems.Add(
                new LegacyCorrespondenceItem()
                {
                    Altinn2CorrespondenceId = correspondence.Altinn2CorrespondenceId,
                    IsConfirmationNeeded = correspondence.IsConfirmationNeeded,
                    ServiceOwnerName = resourceOwners?[correspondence.ResourceId],
                    InstanceOwnerPartyId = recipient?.PartyId ?? 0,
                    MessageTitle = correspondence.Content.MessageTitle,
                    Status = correspondence.GetHighestStatusWithoutPurged().Status,
                    CorrespondenceId = correspondence.Id,
                    MinimumAuthenticationLevel = (int)minAuthLevel,
                    Published = correspondence.Published,
                    PurgedStatus = purgedStatus?.Status,
                    Purged = purgedStatus?.StatusChanged,
                    DueDateTime = correspondence.DueDateTime,
                    Archived = correspondence.Statuses?.FirstOrDefault(s => s.Status == CorrespondenceStatus.Archived)?.StatusChanged,
                    ConfirmationDate = correspondence.Statuses?.FirstOrDefault(s => s.Status == CorrespondenceStatus.Confirmed)?.StatusChanged,
                    MessageSender = String.IsNullOrWhiteSpace(correspondence.MessageSender) ? sender!.Name : correspondence.MessageSender,
                }
                );
        }
        var response = new LegacyGetCorrespondencesResponse
        {
            Items = correspondenceItems,
        };
        return response;
    }

    private static string GetPrefixedForPerson(string ssn)
    {
        return $"{UrnConstants.PersonIdAttribute}:{ssn}";
    }

    private static string GetPrefixedForOrg(string orgnr)
    {
        return $"{UrnConstants.OrganizationNumberAttribute}:{orgnr}";
    }
}