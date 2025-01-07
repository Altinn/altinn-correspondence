namespace Altinn.Correspondence.Core.Services
{
    public interface IContactReservationRegistryService
    {
        public Task<bool> IsPersonReserved(string ssn);

        public Task<List<string>> GetReservedRecipients(List<string> recipients);
    }
}
