namespace Altinn.Correspondence.Core.Services
{
    public interface IContactReservationRegistryService
    {
        public Task<bool> IsPersonReserved(string ssn);
    }
}
