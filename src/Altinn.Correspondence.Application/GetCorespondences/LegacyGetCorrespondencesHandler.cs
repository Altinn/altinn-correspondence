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
    private readonly IAltinnRegisterService _altinnRegisterService;
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly UserClaimsHelper _userClaimsHelper;


    public LegacyGetCorrespondencesHandler(IAltinnAuthorizationService altinnAuthorizationService, ICorrespondenceRepository correspondenceRepository, UserClaimsHelper userClaimsHelper, IAltinnRegisterService altinnRegisterService)
    {
        _altinnAuthorizationService = altinnAuthorizationService;
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
        if(userParty == null)
        {
            return Errors.CouldNotFindOrgNo; // TODO: Update to better error message
        }

        // Verfiy and map partyId for instanceowners
        if (request.InstanceOwnerPartyIdList == null || request.InstanceOwnerPartyIdList.Length == 0)
        {
            return Errors.CouldNotFindOrgNo; // TODO: Update to better error message
        }
        var recipients = new List<string>();
        foreach(int instanceOwnerPartyId in request.InstanceOwnerPartyIdList)
        {
            var mappedInstanceOwner = await _altinnRegisterService.LookUpParty(instanceOwnerPartyId, cancellationToken);
            if(mappedInstanceOwner == null)
            {
                return Errors.CouldNotFindOrgNo; // TODO: Update to better error message
            }
            recipients.Add(mappedInstanceOwner.OrgNumber);
        }

        // TODO: Get All Parties this user can represent
        // Get Authorized Parties
        //   https://docs.altinn.studio/api/accessmanagement/resourceowneropenapi/#/Authorized%20Parties/post_resourceowner_authorizedparties
        //   https://github.com/Altinn/altinn-resource-registry/blob/main/src/Altinn.ResourceRegistry/Controllers/ResourceController.cs#L258
        List<string> recipientIds = new List<string>();

        // TODO: Get All Resources these parties can access
        //   https://docs.altinn.studio/api/resourceregistry/spec/#/Resource/post_resource_bysubjects
        //   https://digdir.slack.com/archives/D07CXBW9AJH/p1727966248268839?thread_ts=1727960943.538609&cid=D07CXBW9AJH
        List<string> resourcesToSearch = new List<string>();

        // Get all correspondences owned by Recipients
        var correspondences = await _correspondenceRepository.GetCorrespondencesForParties(request.Offset, limit, from, to, request.Status, recipientIds, resourcesToSearch, request.Language, request.IncludeActive, request.IncludeArchived, request.IncludeDeleted, request.SearchString, cancellationToken);


        // TODO: Authorize each correspondence using multirequests
        //  https://docs.altinn.studio/authorization/guides/xacml/#request-for-multiple-decisions
        //  https://docs.altinn.studio/api/authorization/spec/#/Decision/post_authorize
        //   Filter out where authorization failed
        //   Enrich with minimum authentication level where successfull
        //foreach(var correspondence in correspondences)
        //{
        //    var hasAccess = await _altinnAuthorizationService.CheckUserAccess(correspondence.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.See }, cancellationToken);
        //}

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