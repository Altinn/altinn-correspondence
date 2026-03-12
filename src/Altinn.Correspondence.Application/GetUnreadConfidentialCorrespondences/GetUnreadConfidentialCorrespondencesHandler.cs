using System.Security.Claims;
using Altinn.Correspondence.Application.UnreadConfidentialCorrespondenceReminder;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.Extensions.Logging;
using OneOf;

namespace Altinn.Correspondence.Application.GetUnreadConfidentialCorrespondences;

public class GetUnreadConfidentialCorrespondencesHandler(
    ILogger<GetUnreadConfidentialCorrespondencesHandler> logger,
    ICorrespondenceRepository correspondenceRepository,
    IAltinnAuthorizationService altinnAuthorizationService
    )
{
    public async Task<OneOf<GetUnreadConfidentialCorrespondencesResponse, Error>> Process(ClaimsPrincipal user, CancellationToken cancellationToken = default)
{
    var recipientOrg = user.GetCallerOrganizationId();
    if (string.IsNullOrEmpty(recipientOrg))
    {
        logger.LogWarning("Access denied for user {caller} - user does not have an organization id claim", recipientOrg);
        return AuthorizationErrors.CouldNotDetermineCaller;
    }
    var hasAccess = await altinnAuthorizationService.CheckAccessAsAny(
            user,
            "correspondence-attachment-test",
            recipientOrg,
            cancellationToken);

    if (!hasAccess)
    {
        return AuthorizationErrors.NoAccessToResource;
    }
    var correspondences = await correspondenceRepository.GetUnopenedConfidentialCorrespondencesForParty(
        recipientOrg.WithUrnPrefix(), 
        cancellationToken
    );

    if (correspondences == null)
    {
        logger.LogError("Error getting unopened confidential correspondences for party {partyId}", recipientOrg);
        throw new Exception("Failed to retrieve unopened confidential correspondences");
    }

    var defaultText = "Under ligger en oversikt over hvilke meldinger som er uåpnet og viser til avsender, dato meldingen ble publisert og hvilken tilgang som kreves. Hovedadministrator må delegere denne tilgangen for at noen i din virksomhet skal kunne se meldingene. Se mer informasjon på våre hjelpesider: https://info.altinn.no/nyheter/tilgang-til-taushetsbelagt-post/";

    var lines = correspondences
        .OrderBy(c => c.Published)
        .Select((c, i) => $"{i + 1}. Melding fra avsender {c.Sender.WithoutPrefix()} datert {c.Published:dd.MM.yyyy}, denne krever tilgang til {c.ResourceId}")
        .ToList();

    var ending = "NB! Dette varselet forsvinner når alle uleste taushetsbelagte meldinger er åpnet.";

    var fullText = defaultText + "\n\n" + string.Join("\n\n", lines) + "\n\n" + ending;

    var response = new GetUnreadConfidentialCorrespondencesResponse
    {
        Text = fullText
    };
    return response;
}
}
