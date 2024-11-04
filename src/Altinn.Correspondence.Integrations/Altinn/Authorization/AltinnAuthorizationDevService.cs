using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;

namespace Altinn.Correspondence.Integrations.Altinn.Authorization
{
    public class AltinnAuthorizationDevService : IAltinnAuthorizationService
    {
        public Task<bool> CheckMigrationAccess(string resourceId, List<ResourceAccessLevel> rights, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<bool> CheckUserAccess(string resourceId, List<ResourceAccessLevel> rights, CancellationToken cancellationToken = default, string? recipientOrgNo = null)
        {
            return Task.FromResult(true);
        }

        public Task<int?> CheckUserAccessAndGetMinimumAuthLevel(string resourceId, List<ResourceAccessLevel> rights, CancellationToken cancellationToken = default, string? recipientOrgNo = null)
        {
            return Task.FromResult((int?)3);
        }
    }
}
