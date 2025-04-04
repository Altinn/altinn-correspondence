using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using System.Security.Claims;

namespace Altinn.Correspondence.Integrations.Altinn.Authorization
{
    public class AltinnAuthorizationDevService : IAltinnAuthorizationService
    {
        public Task<bool> CheckAccessAsAny(ClaimsPrincipal? user, string resource, string party, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public Task<bool> CheckAccessAsRecipient(ClaimsPrincipal? user, CorrespondenceEntity correspondence, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<bool> CheckAccessAsSender(ClaimsPrincipal? user, CorrespondenceEntity correspondence, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<bool> CheckAccessAsSender(ClaimsPrincipal? user, string resourceId, string sender, string? instance, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<int?> CheckUserAccessAndGetMinimumAuthLevel(ClaimsPrincipal? user, string ssn, string resourceId, List<ResourceAccessLevel> rights, string recipientOrgNo, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((int?)3);
        }
    }
}
