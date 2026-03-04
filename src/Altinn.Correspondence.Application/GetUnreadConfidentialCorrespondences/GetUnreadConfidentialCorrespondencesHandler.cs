using System.Security.Claims;
using Altinn.Correspondence.Application.UnreadConfidentialCorrespondenceReminder;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.Extensions.Logging;
using OneOf;

namespace Altinn.Correspondence.Application.GetUnreadConfidentialCorrespondences;

public class GetUnreadConfidentialCorrespondencesHandler(
    ILogger<GetUnreadConfidentialCorrespondencesHandler> logger,
    ICorrespondenceRepository correspondenceRepository)
{
    public async Task<OneOf<GetUnreadConfidentialCorrespondencesResponse, Error>> Process(ClaimsPrincipal user, CancellationToken cancellationToken = default)
{
    var correspondences = await correspondenceRepository.GetUnopenedConfidentialCorrespondencesForParty(
        user.GetCallerOrganizationId().WithUrnPrefix(), 
        cancellationToken
    );

    if (correspondences == null)
    {
        logger.LogError("Error getting unopened confidential correspondences for party {partyId}", user.GetCallerOrganizationId());
        throw new Exception("Failed to retrieve unopened confidential correspondences");
    }

    var response = new GetUnreadConfidentialCorrespondencesResponse
    {
        UnopenedConfidentialCorrespondences = correspondences.Select(c => new ConfidentialCorrespondenceResponse
        {
            Created = c.Created,
            Sender = c.Sender.WithoutPrefix(),
            corrId = c.Id,
            ResourceId = c.ResourceId
        }).ToList()
    };

    return response;
}

}