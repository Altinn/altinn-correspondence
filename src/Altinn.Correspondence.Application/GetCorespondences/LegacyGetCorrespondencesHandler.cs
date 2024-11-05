using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.GetCorrespondences;

public class LegacyGetCorrespondencesHandler : IHandler<LegacyGetCorrespondencesRequest, LegacyGetCorrespondencesResponse>
{
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;
    private readonly IAltinnAccessManagementService _altinnAccessManagementService;
    private readonly IAltinnRegisterService _altinnRegisterService;
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly IResourceRightsService _resourceRightsService;
    private readonly UserClaimsHelper _userClaimsHelper;
    private record ResourceOwner(string OrgNumber, Party? Party);


    public LegacyGetCorrespondencesHandler(IAltinnAuthorizationService altinnAuthorizationService, IAltinnAccessManagementService altinnAccessManagement, ICorrespondenceRepository correspondenceRepository, UserClaimsHelper userClaimsHelper, IAltinnRegisterService altinnRegisterService, IResourceRightsService resourceRightsService)
    {
        _altinnAuthorizationService = altinnAuthorizationService;
        _altinnAccessManagementService = altinnAccessManagement;
        _correspondenceRepository = correspondenceRepository;
        _userClaimsHelper = userClaimsHelper;
        _altinnRegisterService = altinnRegisterService;
        _resourceRightsService = resourceRightsService;
    }

    public async Task<OneOf<LegacyGetCorrespondencesResponse, Error>> Process(LegacyGetCorrespondencesRequest request, CancellationToken cancellationToken)
    {
        var limit = request.Limit == 0 ? 50 : request.Limit;
        DateTimeOffset? to = request.To != null ? ((DateTimeOffset)request.To).ToUniversalTime() : null;
        DateTimeOffset? from = request.From != null ? ((DateTimeOffset)request.From).ToUniversalTime() : null;

        // Verify and map partyId for user
        if (request.OnbehalfOfPartyId == 0 || request.OnbehalfOfPartyId == int.MinValue)
        {
            return Errors.CouldNotFindOrgNo; // TODO: Update to better error message
        }
        var userParty = await _altinnRegisterService.LookUpPartyByPartyId(request.OnbehalfOfPartyId, cancellationToken);
        if (userParty == null || (string.IsNullOrEmpty(userParty.SSN) && string.IsNullOrEmpty(userParty.OrgNumber)))
        {
            return Errors.CouldNotFindOrgNo; // TODO: Update to better error message
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
            recipients.Add(string.IsNullOrEmpty(userParty.SSN) ? "0192:" + userParty.OrgNumber : userParty.SSN);
        }

        List<string> resourcesToSearch = new List<string>();

        // Get all correspondences owned by Recipients
        var correspondences = await _correspondenceRepository.GetCorrespondencesForParties(request.Offset, limit, from, to, request.Status, recipients, resourcesToSearch, request.Language, request.IncludeActive, request.IncludeArchived, request.IncludeDeleted, request.SearchString, cancellationToken);

        Console.WriteLine($"Found {correspondences.Item1.Count} correspondences");

        var resourceIds = correspondences.Item1.Select(c => c.ResourceId).Distinct().ToList();
        var authorizedCorrespondences = new List<CorrespondenceEntity>();
        List<LegacyCorrespondenceItem> correspondenceItems = new List<LegacyCorrespondenceItem>();

        var resourceOwners = new List<ResourceOwner>();
        foreach (var orgNr in correspondences.Item1.Select(c => c.Sender).Distinct().ToList())
        {
            try
            {
                var resourceOwnerParty = await _altinnRegisterService.LookUpPartyById(orgNr, cancellationToken);
                resourceOwners.Add(new ResourceOwner(orgNr, resourceOwnerParty));
            }
            catch (Exception e)
            {
                resourceOwners.Add(new ResourceOwner(orgNr, null));
            }
        }
        foreach (var correspondence in correspondences.Item1)
        {
            var purgedStatus = correspondence.GetPurgedStatus();
            var owner = resourceOwners.SingleOrDefault(r => r.OrgNumber == correspondence.Sender)?.Party;
            correspondenceItems.Add(
                new LegacyCorrespondenceItem()
                {
                    Altinn2CorrespondenceId = correspondence.Altinn2CorrespondenceId,
                    ServiceOwnerName = owner.Name,
                    InstanceOwnerPartyId = owner.PartyId,
                    MessageTitle = correspondence.Content.MessageTitle,
                    Status = correspondence.GetLatestStatusWithoutPurged().Status,
                    CorrespondenceId = correspondence.Id,
                    MinimumAuthenticationlevel = 0, // Insert from response from PDP multirequest
                    Published = correspondence.Published,
                    PurgedStatus = purgedStatus?.Status,
                    Purged = purgedStatus?.StatusChanged,
                    DueDateTime = correspondence.DueDateTime,
                    Archived = correspondence.Statuses?.FirstOrDefault(s => s.Status == CorrespondenceStatus.Archived)?.StatusChanged,
                    Confirmed = correspondence.Statuses?.FirstOrDefault(s => s.Status == CorrespondenceStatus.Confirmed)?.StatusChanged,
                    MessageSender = correspondence.MessageSender
                }
                );
        }
        Console.WriteLine($"Finished correspondences: {correspondenceItems.Count}");
        var response = new LegacyGetCorrespondencesResponse
        {
            Items = correspondenceItems,
            Pagination = new PaginationMetaData
            {
                Offset = request.Offset,
                Limit = limit,
                TotalItems = correspondences.Item2
            }
        };
        return response;
    }
}

// TODO: Get All Resources these parties can access. I do think these resources is included in authorized parties response
//   <https://docs.altinn.studio/api/resourceregistry/spec/#/Resource/post_resource_bysubjects>
//   https://digdir.slack.com/archives/D07CXBW9AJH/p1727966248268839?thread_ts=1727960943.538609&cid=D07CXBW9AJH

// TODO: Authorize each correspondence using multirequests
//  https://docs.altinn.studio/authorization/guides/xacml/#request-for-multiple-decisions
//  https://docs.altinn.studio/api/authorization/spec/#/Decision/post_authorize
//   Filter out where authorization failed
//   Enrich with minimum authentication level where successfull