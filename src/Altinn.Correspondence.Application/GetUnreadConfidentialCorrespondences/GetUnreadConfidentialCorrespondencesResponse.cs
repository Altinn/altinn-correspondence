using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Application.UnreadConfidentialCorrespondenceReminder;

public class GetUnreadConfidentialCorrespondencesResponse
{
    public List<ConfidentialCorrespondenceResponse> UnopenedConfidentialCorrespondences { get; set; } = new List<ConfidentialCorrespondenceResponse>();
    public string DefaultText { get; set; } = $"Din virksomhet har mottatt taushetsbelagt post fra følgende virksomheter. For å se denne meldingen kreves tilgang til ressursene. Hovedadministrator må delegere denne tilgangen for at noen skal kunne se denne meldingen. Se mer informasjon på våre hjelpesider: https://info.altinn.no/nyheter/tilgang-til-taushetsbelagt-post/";
}

public class ConfidentialCorrespondenceResponse
{
    public string? Sender { get; set; }
    public DateTimeOffset Created { get; set; }
    public Guid corrId { get; set; }
    public string? ResourceId { get; set; }
    public string Message => $"{Sender} datert {Created:dd.MM.yyyy}, denne krever tilgang til {ResourceId}";
}