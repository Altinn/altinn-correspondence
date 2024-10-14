using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Altinn.Register;
using Altinn.Platform.Register.Models;
using Microsoft.Extensions.Azure;
using Microsoft.IdentityModel.Tokens;
using OneOf;

namespace Altinn.Correspondence.Application.GetCorrespondences;

public class LegacyGetCorrespondencesHandler : IHandler<LegacyGetCorrespondencesRequest, LegacyGetCorrespondencesResponse>
{
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;
    private readonly IAltinnAccessManagementService _altinnAccessManagementService;
    private readonly IAltinnRegisterService _altinnRegisterService;
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly UserClaimsHelper _userClaimsHelper;


    public LegacyGetCorrespondencesHandler(IAltinnAuthorizationService altinnAuthorizationService, IAltinnAccessManagementService altinnAccessManagement, ICorrespondenceRepository correspondenceRepository, UserClaimsHelper userClaimsHelper, IAltinnRegisterService altinnRegisterService)
    {
        _altinnAuthorizationService = altinnAuthorizationService;
        _altinnAccessManagementService = altinnAccessManagement;
        _correspondenceRepository = correspondenceRepository;
        _userClaimsHelper = userClaimsHelper;
        _altinnRegisterService = altinnRegisterService;
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

        var recipients = new List<string>();
        if (request.InstanceOwnerPartyIdList == null || request.InstanceOwnerPartyIdList.Length == 0)
        {
            recipients.Add(userParty.OrgNumber);
        }
        foreach (int instanceOwnerPartyId in request.InstanceOwnerPartyIdList)
        {
            var mappedInstanceOwner = await _altinnRegisterService.LookUpParty(instanceOwnerPartyId, cancellationToken);
            if (mappedInstanceOwner == null)
            {
                return Errors.CouldNotFindOrgNo; // TODO: Update to better error message
            }
            if (mappedInstanceOwner.OrgNumber != null)
                recipients.Add(mappedInstanceOwner.OrgNumber);
            if (mappedInstanceOwner.SSN != null)
                recipients.Add(mappedInstanceOwner.SSN);
        }

        var parties = await _altinnAccessManagementService.GetAutorizedParties(userParty, cancellationToken);
        var authorizedResources = new List<string>();
        List<string> recipientIds = new List<string>();
        foreach (var party in parties)
        {
            if (party.Resources != null) authorizedResources.AddRange(party.Resources);
        }
        authorizedResources = authorizedResources.Distinct().ToList();
        List<string> resourcesToSearch = new List<string>();

        // Get all correspondences owned by Recipients
        var correspondences = await _correspondenceRepository.GetCorrespondencesForParties(request.Offset, limit, from, to, request.Status, recipients, resourcesToSearch, request.Language, request.IncludeActive, request.IncludeArchived, request.IncludeDeleted, request.SearchString, cancellationToken);

        var resourceIds = correspondences.Item1.Select(c => c.ResourceId).Distinct().ToList();
        foreach (var resource in resourceIds)
        {
            if (!authorizedResources.Contains(resource))
            {
                // Remove all correspondences for this resource
                correspondences.Item1.RemoveAll(c => c.ResourceId == resource);
            }
        }

        List<LegacyCorrespondenceItem> correspondenceItems = new List<LegacyCorrespondenceItem>();
        foreach (var correspondence in correspondences.Item1)
        {
            correspondenceItems.Add(
                new LegacyCorrespondenceItem()
                {
                    Altinn2CorrespondenceId = correspondence.Altinn2CorrespondenceId,
                    ServiceOwnerName = correspondence.MessageSender, // Find alternative source
                    MessageTitle = correspondence.Content.MessageTitle,
                    Status = correspondence.GetLatestStatus().Status,
                    CorrespondenceId = correspondence.Id,
                    MinimumAuthenticationlevel = 0 // Insert from response from PDP multirequest
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
                TotalItems = correspondences.Item2
            }
        };
        return response;
    }
}
// Get Authorized Parties
//   https://docs.altinn.studio/api/accessmanagement/resourceowneropenapi/#/Authorized%20Parties/post_resourceowner_authorizedparties
//   https://github.com/Altinn/altinn-resource-registry/blob/main/src/Altinn.ResourceRegistry/Controllers/ResourceController.cs#L258


// TODO: Get All Resources these parties can access
//   https://docs.altinn.studio/api/resourceregistry/spec/#/Resource/post_resource_bysubjects
//   https://digdir.slack.com/archives/D07CXBW9AJH/p1727966248268839?thread_ts=1727960943.538609&cid=D07CXBW9AJH

// TODO: Authorize each correspondence using multirequests
//  https://docs.altinn.studio/authorization/guides/xacml/#request-for-multiple-decisions
//  https://docs.altinn.studio/api/authorization/spec/#/Decision/post_authorize
//   Filter out where authorization failed
//   Enrich with minimum authentication level where successfull