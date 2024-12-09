using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Persistence.Repositories
{
    public class LegacyPartyRepository(ApplicationDbContext context) : ILegacyPartyRepository
    {
        private readonly ApplicationDbContext _context = context;

        public async Task AddLegacyPartyId(int id, CancellationToken cancellationToken)
        {
            await _context.LegacyParties.AddAsync(new LegacyPartyEntity { PartyId = id }, cancellationToken);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> PartyAlreadyExists(int partyId, CancellationToken cancellationToken)
        {
            return await _context.LegacyParties.AnyAsync(p => p.PartyId == partyId);
        }
    }
}