using Altinn.Correspondence.Core.Services;

namespace Altinn.Correspondence.Integrations.Altinn.ContactReservationRegistry;

internal class ContactReservationRegistryDevService : IContactReservationRegistryService
{
    public Task<bool> IsPersonReserved(string ssn)
    {
        return Task.FromResult(false);
    }

    public Task<List<string>> GetReservedRecipients(List<string> recipients)
    {
        return Task.FromResult(new List<string>());
    }
}
