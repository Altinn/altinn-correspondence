using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Correspondence.Integrations.Dialogporten
{
    internal class DialogportenDevService : IDialogportenService
    {
        public Task<string> CreateCorrespondenceDialog(Guid correspondenceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Guid.NewGuid().ToString());
        }

        public Task CreateCorrespondenceStatusUpdateDialogActivity(Guid correspondenceId, CorrespondenceStatus status, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
