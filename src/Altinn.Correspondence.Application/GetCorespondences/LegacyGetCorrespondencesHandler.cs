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
        var userParty = await _altinnRegisterService.LookUpParty(request.OnbehalfOfPartyId, cancellationToken);
        if (userParty == null)
        {
            return Errors.CouldNotFindOrgNo; // TODO: Update to better error message
        }
        Console.WriteLine($"UserParty: {userParty.OrgNumber}");
        var recipients = new List<string>();
        if (request.InstanceOwnerPartyIdList == null || request.InstanceOwnerPartyIdList.Length == 0)
        {
            if (userParty.OrgNumber != null) recipients.Add("0192:" + userParty.OrgNumber);
            else if (userParty.SSN != null) recipients.Add(userParty.SSN);
        }
        foreach (int instanceOwnerPartyId in request.InstanceOwnerPartyIdList)
        {
            var mappedInstanceOwner = await _altinnRegisterService.LookUpParty(instanceOwnerPartyId, cancellationToken);
            if (mappedInstanceOwner == null)
            {
                return Errors.CouldNotFindOrgNo; // TODO: Update to better error message
            }
            if (mappedInstanceOwner.OrgNumber != null)
                recipients.Add("0192:" + mappedInstanceOwner.OrgNumber);
            else if (mappedInstanceOwner.SSN != null)
                recipients.Add(mappedInstanceOwner.SSN);
        }
        Console.WriteLine($"Recipients: {recipients.Count}");

        foreach (var recipient in recipients)
        {
            Console.WriteLine($"Recipient: {recipient}");
        }
        /*var parties = await _altinnAccessManagementService.GetAutorizedParties(userParty, cancellationToken);
        var authorizedResources = new List<string>();
        List<string> recipientIds = new List<string>();
        foreach (var party in parties)
        {
            if (party.Resources != null) authorizedResources.AddRange(party.Resources);
        }
        authorizedResources = authorizedResources.Distinct().ToList();
        */
        List<string> resourcesToSearch = new List<string>();

        // Get all correspondences owned by Recipients
        var correspondences = await _correspondenceRepository.GetCorrespondencesForParties(request.Offset, limit, from, to, request.Status, recipients, resourcesToSearch, request.Language, request.IncludeActive, request.IncludeArchived, request.IncludeDeleted, request.SearchString, cancellationToken);

        Console.WriteLine($"Found {correspondences.Item1.Count} correspondences");

        var resourceIds = correspondences.Item1.Select(c => c.ResourceId).Distinct().ToList();
        var authorizedCorrespondences = new List<CorrespondenceEntity>();
        foreach (var correspondence in correspondences.Item1)
        {
            var hasAccess = await _altinnAuthorizationService.CheckUserAccess(correspondence.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Read }, cancellationToken, "0192:" + userParty.OrgNumber);
            if (hasAccess)
            {
                authorizedCorrespondences.Add(correspondence);
            }
        }
        Console.WriteLine($"Authorized correspondences: {authorizedCorrespondences.Count}");
        List<LegacyCorrespondenceItem> correspondenceItems = new List<LegacyCorrespondenceItem>();
        var resourceOwners = new List<Tuple<string, string>>();
        foreach (var orgNr in correspondences.Item1.Select(c => c.Sender).Distinct().ToList())
        {
            try
            {
                var resourceOwnerParty = await _altinnRegisterService.LookUpName(orgNr, cancellationToken);
                resourceOwners.Add(new Tuple<string, string>(orgNr, resourceOwnerParty));
            }
            catch (Exception e)
            {
                resourceOwners.Add(new Tuple<string, string>(orgNr, "Temporary name"));
            }
        }
        foreach (var correspondence in authorizedCorrespondences)
        {

            correspondenceItems.Add(
                new LegacyCorrespondenceItem()
                {
                    Altinn2CorrespondenceId = correspondence.Altinn2CorrespondenceId,
                    ServiceOwnerName = resourceOwners.SingleOrDefault(r => r.Item1 == correspondence.Sender)?.Item2,
                    MessageTitle = correspondence.Content.MessageTitle,
                    Status = correspondence.GetLatestStatus().Status,
                    CorrespondenceId = correspondence.Id,
                    MinimumAuthenticationlevel = 0 // Insert from response from PDP multirequest
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
// Get Authorized Parties
//   https://docs.altinn.studio/api/accessmanagement/resourceowneropenapi/#/Authorized%20Parties/post_resourceowner_authorizedparties
//   https://github.com/Altinn/altinn-resource-registry/blob/main/src/Altinn.ResourceRegistry/Controllers/ResourceController.cs#L258


// TODO: Get All Resources these parties can access. I do think these resources is included in authorized parties response
//   <https://docs.altinn.studio/api/resourceregistry/spec/#/Resource/post_resource_bysubjects>
//   https://digdir.slack.com/archives/D07CXBW9AJH/p1727966248268839?thread_ts=1727960943.538609&cid=D07CXBW9AJH

// TODO: Authorize each correspondence using multirequests
//  https://docs.altinn.studio/authorization/guides/xacml/#request-for-multiple-decisions
//  https://docs.altinn.studio/api/authorization/spec/#/Decision/post_authorize
//   Filter out where authorization failed
//   Enrich with minimum authentication level where successfull