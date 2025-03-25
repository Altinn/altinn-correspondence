using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Correspondence.Persistence.Repositories
{
    internal class CorrespondenceForwardingEventRepository(ApplicationDbContext context) : ICorrespondenceForwardingEventRepository
    {
        public Task<Guid> AddForwardingEvent(CorrespondenceForwardingEventEntity forwardingEvent, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<List<CorrespondenceForwardingEventEntity>> GetForwardingEvents(Guid correspondenceId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
