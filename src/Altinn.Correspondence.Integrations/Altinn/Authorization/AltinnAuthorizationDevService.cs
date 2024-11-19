using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using System.Security.Claims;

namespace Altinn.Correspondence.Integrations.Altinn.Authorization
{
    public class AltinnAuthorizationDevService : IAltinnAuthorizationService
    {
        public Task<bool> CheckMigrationAccess(string resourceId, List<ResourceAccessLevel> rights, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<bool> CheckUserAccess(ClaimsPrincipal? user, string resourceId, List<ResourceAccessLevel> rights, CancellationToken cancellationToken = default, string? recipientOrgNo = null)
        {
            return Task.FromResult(true);
        }

        public Task<int?> CheckUserAccessAndGetMinimumAuthLevel(ClaimsPrincipal? user, string ssn, string resourceId, List<ResourceAccessLevel> rights, string recipientOrgNo, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((int?)3);
        }
    }
}
