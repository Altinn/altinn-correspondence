using Altinn.Correspondence.Application.Settings;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Services;

namespace Altinn.Correspondence.Application.Helpers
{
    public static class AltinnRegisterServiceExtensions
    {
        public static async Task<bool> HasRequiredOrganizationRoles(this IAltinnRegisterService altinnRegisterService, string partyUuid, bool isConfidential, CancellationToken cancellationToken)
        {
            var roles = await altinnRegisterService.LookUpPartyRoles(partyUuid, cancellationToken);
            if (isConfidential)
            {
                return roles.Any(r => ApplicationConstants.RequiredOrganizationRolesForConfidentialCorrespondenceRecipient.Contains(r.Role.Identifier));
            }
            return roles.Any(r => ApplicationConstants.RequiredOrganizationRolesForCorrespondenceRecipient.Contains(r.Role.Identifier));
        }

        public static async Task<bool> HasPartyRequiredRoles(this IAltinnRegisterService altinnRegisterService, string recipientUrn, Guid partyUuid, bool isConfidential, CancellationToken cancellationToken)
        {
            var mainUnits = await altinnRegisterService.LookUpMainUnits(recipientUrn.WithUrnPrefix(), cancellationToken);
            if (!mainUnits.Any())
            {
                return await altinnRegisterService.HasRequiredOrganizationRoles(partyUuid.ToString(), isConfidential, cancellationToken);
            }
            else
            {
                return await altinnRegisterService.HasRequiredOrganizationRoles(mainUnits.First().PartyUuid.ToString(), isConfidential, cancellationToken);
            }
        }
    }
}
