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
    public async Task<OneOf<GetUnreadConfidentialCorrespondencesResponse, Error>> Process(ClaimsPrincipal user, string languageCode, CancellationToken cancellationToken = default)
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


    var defaultTextNb = "Under ligger en oversikt over hvilke meldinger som er uåpnet og viser til avsender, dato meldingen ble publisert og hvilken tjeneste som kreves. Hovedadministrator må delegere tilgang til denne tjenesten for at noen i din virksomhet skal kunne se meldingene. Se mer informasjon på våre hjelpesider: https://info.altinn.no/help/ny-tilgangsstyring/enkelttjenester/";
    var defaultTextNn = "Under ligg ein oversikt over kva meldingar som er uopna og viser til avsendar, dato meldinga blei publisert og kva teneste som krevst. Hovudadministrator må delegere tilgang til denne tenesta for at nokon i verksemda di skal kunne sjå meldingane. Sjå meir informasjon på våre hjelpesider: https://info.altinn.no/nn/help/ny-tilgangsstyring/enkelttjenester/";
    var defaultTextEn = "Below is an overview of which correspondences are unopened and shows the sender, the date the correspondence was published and which service is required. The main administrator must delegate access to this service for someone in your organization to be able to see the messages. See more information on our support pages: https://info.altinn.no/en/help/ny-tilgangsstyring/enkelttjenester/";

    var defaultText = languageCode switch
    {
        "nb" => defaultTextNb,
        "nn" => defaultTextNn,
        "en" => defaultTextEn,
        _ => defaultTextNb
    };


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

    var linesNb = sortedCorrespondences
        .Select((c, i) => $"{i + 1}. Melding fra avsender {senderNames[i] ?? c.Sender.WithoutPrefix()} datert {c.Published:dd.MM.yyyy}, denne krever tilgang til {c.ResourceId}")
        .ToList();

    var linesNn = sortedCorrespondences
        .Select((c, i) => $"{i + 1}. Melding frå avsendar {senderNames[i] ?? c.Sender.WithoutPrefix()} datert {c.Published:dd.MM.yyyy}, denne krev tilgang til {c.ResourceId}")
        .ToList();

    var linesEn = sortedCorrespondences
        .Select((c, i) => $"{i + 1}. Correspondence from sender {senderNames[i] ?? c.Sender.WithoutPrefix()} dated {c.Published:dd.MM.yyyy}, this requires access to {c.ResourceId}")
        .ToList();

    var lines = languageCode switch
    {
        "nb" => linesNb,
        "nn" => linesNn,
        "en" => linesEn,
        _ => linesNb
    };


    var endingNb = "NB! Dette varselet forsvinner når alle uleste taushetsbelagte meldinger er åpnet.";
    var endingNn = "NB! Dette varselet forsvinn når alle ulesne tausheitsbelagte meldingar er opna.";
    var endingEn = "N.B. This notice will disappear when all unread confidential correspondences have been opened.";

    var ending = languageCode switch
    {
        "nb" => endingNb,
        "nn" => endingNn,
        "en" => endingEn,
        _ => endingNb
    };
    var fullText = defaultText + "\n\n" + string.Join("\n\n", lines) + "\n\n" + ending;

    var response = new GetUnreadConfidentialCorrespondencesResponse
    {
        Text = fullText
    };
    return response;
}
}
