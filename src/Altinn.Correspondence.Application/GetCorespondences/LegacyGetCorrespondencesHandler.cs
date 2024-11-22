using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Repositories;
using OneOf;

using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.GetCorrespondences;

public class LegacyGetCorrespondencesHandler(IAltinnAuthorizationService altinnAuthorizationService, IAltinnAccessManagementService altinnAccessManagement, ICorrespondenceRepository correspondenceRepository, UserClaimsHelper userClaimsHelper, IAltinnRegisterService altinnRegisterService, IResourceRightsService resourceRightsService, ILogger<LegacyGetCorrespondencesHandler> logger) : IHandler<LegacyGetCorrespondencesRequest, LegacyGetCorrespondencesResponse>
{
    private readonly IAltinnAuthorizationService _altinnAuthorizationService = altinnAuthorizationService;
    private readonly IAltinnAccessManagementService _altinnAccessManagementService = altinnAccessManagement;
    private readonly IAltinnRegisterService _altinnRegisterService = altinnRegisterService;
    private readonly ICorrespondenceRepository _correspondenceRepository = correspondenceRepository;
    private readonly IResourceRightsService _resourceRightsService = resourceRightsService;
    private readonly UserClaimsHelper _userClaimsHelper = userClaimsHelper;
    private readonly ILogger<LegacyGetCorrespondencesHandler> _logger = logger;
    private record PartyInfo(string Id, Party? Party);

    public async Task<OneOf<LegacyGetCorrespondencesResponse, Error>> Process(LegacyGetCorrespondencesRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var limit = request.Limit == 0 ? 50 : request.Limit;
        DateTimeOffset? to = request.To != null ? ((DateTimeOffset)request.To).ToUniversalTime() : null;
        DateTimeOffset? from = request.From != null ? ((DateTimeOffset)request.From).ToUniversalTime() : null;

        if (from != null && to != null && from > to)
        {
            return Errors.InvalidDateRange;
        }
        if (_userClaimsHelper.GetPartyId() is not int partyId)
        {
            return Errors.InvalidPartyId;
        }
        var minAuthLevel = _userClaimsHelper.GetMinimumAuthenticationLevel();
        var userParty = await _altinnRegisterService.LookUpPartyByPartyId(partyId, cancellationToken);
        if (userParty == null || (string.IsNullOrEmpty(userParty.SSN) && string.IsNullOrEmpty(userParty.OrgNumber)))
        {
            return Errors.CouldNotFindOrgNo;
        }
        var recipients = new List<string>();
        if (request.InstanceOwnerPartyIdList != null && request.InstanceOwnerPartyIdList.Length > 0)
        {
            var authorizedParties = await _altinnAccessManagementService.GetAuthorizedParties(userParty, cancellationToken);
            var authorizedPartiesDict = authorizedParties.ToDictionary(c => c.PartyId);
            foreach (int instanceOwnerPartyId in request.InstanceOwnerPartyIdList)
            {
                if (!authorizedPartiesDict.TryGetValue(instanceOwnerPartyId, out var mappedInstanceOwner))
                {
                    return Errors.LegacyNotAccessToOwner(instanceOwnerPartyId);
                }
                if (mappedInstanceOwner.OrgNumber != null)
                    recipients.Add("0192:" + mappedInstanceOwner.OrgNumber);
                else if (mappedInstanceOwner.SSN != null)
                    recipients.Add(mappedInstanceOwner.SSN);
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(userParty.SSN)) recipients.Add(userParty.SSN);
            if (!string.IsNullOrEmpty(userParty.OrgNumber)) recipients.Add("0192:" + userParty.OrgNumber);
        }
        List<string> resourcesToSearch = new List<string>();

        // Get all correspondences owned by Recipients
        var correspondences = await _correspondenceRepository.GetCorrespondencesForParties(request.Offset, limit, from, to, request.Status, recipients, resourcesToSearch, request.IncludeActive, request.IncludeArchived, request.IncludeDeleted, request.SearchString, cancellationToken);

        var resourceIds = correspondences.Item1.Select(c => c.ResourceId).Distinct().ToList();
        var authorizedCorrespondences = new List<CorrespondenceEntity>();
        List<LegacyCorrespondenceItem> correspondenceItems = new List<LegacyCorrespondenceItem>();

        var Senders = new List<PartyInfo>();
        foreach (var orgNr in correspondences.Item1.Select(c => c.Sender).Distinct().ToList())
        {
            try
            {
                var resourceOwnerParty = await _altinnRegisterService.LookUpPartyById(orgNr, cancellationToken);
                Senders.Add(new PartyInfo(orgNr, resourceOwnerParty));
            }
            catch (Exception e)
            {
                Senders.Add(new PartyInfo(orgNr, null));
            }
        }

        var resourceOwners = new List<PartyInfo>();
        foreach (var resource in correspondences.Item1.Select(c => c.ResourceId).Distinct().ToList())
        {
            try
            {
                var resourceOwner = await _resourceRightsService.GetServiceOwnerOfResource(resource, cancellationToken);
                var resourceOwnerInfo = await _altinnRegisterService.LookUpPartyById(resourceOwner, cancellationToken);
                resourceOwners.Add(new PartyInfo(resource, resourceOwnerInfo));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to lookup resource owner for resource: {resource}", resource);
                resourceOwners.Add(new PartyInfo(resource, null));
            }
        }

        var recipientDetails = new List<PartyInfo>();
        foreach (var orgNr in correspondences.Item1.Select(c => c.Recipient).Distinct().ToList())
        {
            try
            {
                var recipientParty = await _altinnRegisterService.LookUpPartyById(orgNr, cancellationToken);
                recipientDetails.Add(new PartyInfo(orgNr, recipientParty));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to lookup recipient party for orgNr: {OrgNr}", orgNr);
                recipientDetails.Add(new PartyInfo(orgNr, null));
            }
        }
        var correspondenceToSubtractFromTotal = 0;
        foreach (var correspondence in correspondences.Item1)
        {
            var authLevel = await _altinnAuthorizationService.CheckUserAccessAndGetMinimumAuthLevel(user, userParty.SSN, correspondence.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Read }, correspondence.Recipient, cancellationToken);
            if (minAuthLevel == null || minAuthLevel < authLevel)
            {
                correspondenceToSubtractFromTotal++;
                continue;
            }
            var purgedStatus = correspondence.GetPurgedStatus();
            var sender = Senders.SingleOrDefault(r => r.Id == correspondence.Sender)?.Party;
            var recipient = recipientDetails.SingleOrDefault(r => r.Id == correspondence.Recipient)?.Party;
            correspondenceItems.Add(
                new LegacyCorrespondenceItem()
                {
                    Altinn2CorrespondenceId = correspondence.Altinn2CorrespondenceId,
                    ServiceOwnerName = resourceOwners?.SingleOrDefault(r => r.Id == correspondence.ResourceId).Party?.Name,
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
                    Confirmed = correspondence.Statuses?.FirstOrDefault(s => s.Status == CorrespondenceStatus.Confirmed)?.StatusChanged,
                    MessageSender = String.IsNullOrWhiteSpace(correspondence.MessageSender) ? sender!.Name : correspondence.MessageSender,
                }
                );
        }
        var response = new LegacyGetCorrespondencesResponse
        {
            Items = correspondenceItems,
            Pagination = new PaginationMetaData
            {
                Offset = request.Offset,
                Limit = limit,
                TotalItems = correspondences.Item2 - correspondenceToSubtractFromTotal
            }
        };
        return response;
    }
}