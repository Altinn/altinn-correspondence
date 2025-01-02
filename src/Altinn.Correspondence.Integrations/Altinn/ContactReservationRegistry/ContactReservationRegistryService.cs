using Altinn.Correspondence.Core.Services;

namespace Altinn.Correspondence.Integrations.Altinn.ContactReservationRegistry
{
    /**
     * Also known as "Kontakt- og reservasjonsregisteret" ("KRR").
     * */
    public class ContactReservationRegistryService : IContactReservationRegistryService
    {
        public Task<bool> IsPersonReserved(string ssn)
        {
            throw new NotImplementedException();
        }
    }
}
