namespace Altinn.Correspondence.Core.Repositories
{
    public interface ILegacyPartyRepository
    {
        Task AddLegacyPartyId(int id, CancellationToken cancellationToken);
        Task<bool> PartyAlreadyExists(int id, CancellationToken cancellationToken);
    }
}