using Altinn.Correspondence.Application.Settings;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Services;

namespace Altinn.Correspondence.Application.Helpers
{
    public static class AltinnRegisterServiceExtensions
    {
        public static async Task<bool> HasRequiredOrganizationRoles(this IAltinnRegisterService altinnRegisterService, string partyUuid, CancellationToken cancellationToken)
        {
            var roles = await altinnRegisterService.LookUpPartyRoles(partyUuid, cancellationToken);
            return roles.Any(r => ApplicationConstants.RequiredOrganizationRolesForConfidentialCorrespondenceRecipient.Contains(r.Role.Identifier));
        }

        public static async Task<bool> HasPartyRequiredRolesForConfidential(this IAltinnRegisterService altinnRegisterService, string recipientUrn, Guid partyUuid, CancellationToken cancellationToken)
        {
            var mainUnits = await altinnRegisterService.LookUpMainUnits(recipientUrn.WithoutPrefix().WithUrnPrefix(), cancellationToken);
            if (!mainUnits.Any())
            {
                return await altinnRegisterService.HasRequiredOrganizationRoles(partyUuid.ToString(), cancellationToken);
            }
            else
            {
                return await altinnRegisterService.HasRequiredOrganizationRoles(mainUnits.First().PartyUuid.ToString(), cancellationToken);
            }
        }
    }
}
