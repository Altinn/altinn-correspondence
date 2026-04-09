using System.Security.Claims;
using Altinn.Correspondence.Application.UnreadConfidentialCorrespondenceReminder;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Hosting;
using OneOf;

namespace Altinn.Correspondence.Application.GetUnreadConfidentialCorrespondences;

public class GetUnreadConfidentialCorrespondencesHandler(
    ICorrespondenceRepository correspondenceRepository,
    IAltinnAuthorizationService altinnAuthorizationService,
    IAltinnRegisterService altinnRegisterService,
    IHostEnvironment hostEnvironment
    )
{
    public async Task<OneOf<GetUnreadConfidentialCorrespondencesResponse, Error>> Process(ClaimsPrincipal user, CancellationToken cancellationToken = default)
{
    var recipientOrg = user.GetCallerOrganizationId();
    if (string.IsNullOrEmpty(recipientOrg))
    {
        return AuthorizationErrors.CouldNotDetermineCaller;
    }
    var hasAccess = await altinnAuthorizationService.CheckAccessAsAny(
            user,
            "ttd-reminder-unopened-confidential-correspondences",
            recipientOrg,
            cancellationToken);

    if (!hasAccess)
    {
        return AuthorizationErrors.NoAccessToResource;
    }
    var minAge = hostEnvironment.IsProduction()
        ? TimeSpan.FromDays(7) : TimeSpan.FromMinutes(1);
    var correspondences = await correspondenceRepository.GetUnopenedConfidentialCorrespondencesForParty(
        recipientOrg.WithUrnPrefix(),
        minAge,
        cancellationToken
    );

    if (correspondences.Count == 0)
    {
        return CorrespondenceErrors.UnreadConfidentialCorrespondencesNotFound;
    }

    var defaultText = "Under ligger en oversikt over hvilke meldinger som er uåpnet og viser til avsender, dato meldingen ble publisert og hvilken tilgang som kreves. Hovedadministrator må delegere denne tilgangen for at noen i din virksomhet skal kunne se meldingene. Se mer informasjon på våre hjelpesider: https://info.altinn.no/nyheter/tilgang-til-taushetsbelagt-post/";

    var sortedCorrespondences = correspondences.OrderBy(c => c.Published).ToList();
    var senderNames = await Task.WhenAll(
        sortedCorrespondences.Select(async c =>
        {
            if (!string.IsNullOrWhiteSpace(c.MessageSender))
            {
                return c.MessageSender;
            }
            try
            {
                return await altinnRegisterService.LookUpName(c.Sender.WithoutPrefix(), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return null;
            }
        })
    );

    var lines = sortedCorrespondences
        .Select((c, i) => $"{i + 1}. Melding fra avsender {senderNames[i] ?? c.Sender.WithoutPrefix()} datert {c.Published:dd.MM.yyyy}, denne krever tilgang til {c.ResourceId}")
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
