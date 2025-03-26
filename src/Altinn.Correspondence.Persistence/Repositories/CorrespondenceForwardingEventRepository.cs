using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Correspondence.Persistence.Repositories
{
    internal class CorrespondenceForwardingEventRepository(ApplicationDbContext context) : ICorrespondenceForwardingEventRepository
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<Guid> AddForwardingEvent(CorrespondenceForwardingEventEntity forwardingEvent, CancellationToken cancellationToken)
        {
            await _context.CorrespondenceForwardingEvents.AddAsync(forwardingEvent, cancellationToken);
            await _context.SaveChangesAsync();
            return forwardingEvent.Id;
        }

        public async Task<List<CorrespondenceForwardingEventEntity>> GetForwardingEventsForCorrespondenceId(Guid correspondenceId, CancellationToken cancellationToken)
        {
            return _context.CorrespondenceForwardingEvents.Where(e => e.CorrespondenceId == correspondenceId).ToList();
        }
    }
}
