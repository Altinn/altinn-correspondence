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

    var defaultText = "Din virksomhet har mottatt taushetsbelagt post fra følgende virksomheter. For å se denne meldingen kreves tilgang til ressursene. Hovedadministrator må delegere denne tilgangen for at noen skal kunne se denne meldingen. Se mer informasjon på våre hjelpesider: https://info.altinn.no/nyheter/tilgang-til-taushetsbelagt-post/";

    var lines = correspondences
        .OrderBy(c => c.Created)
        .Select((c, i) => $"{i + 1}. {c.Sender.WithoutPrefix()} datert {c.Created:dd.MM.yyyy}, denne krever tilgang til {c.ResourceId}")
        .ToList();

    var fullText = defaultText + "\n\n" + string.Join("\n\n", lines);

    var response = new GetUnreadConfidentialCorrespondencesResponse
    {
        Text = fullText
    };

    return response;
}

}