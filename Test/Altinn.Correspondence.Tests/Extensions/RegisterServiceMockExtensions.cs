using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Register;
using Altinn.Correspondence.Core.Services;
using Moq;

namespace Altinn.Correspondence.Tests.Extensions
{
    internal static class RegisterServiceMockExtensions
    {
        public static Mock<IAltinnRegisterService> SetupPartyRoleLookup(this Mock<IAltinnRegisterService> mockRegisterService, string partyUuidOrMatch, string roleIdentifier)
        {
            mockRegisterService
                .Setup(s => s.LookUpPartyRoles(It.Is<string>(val => val.Contains(partyUuidOrMatch)), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<RoleItem> { new RoleItem { Role = new RoleDescriptor { Identifier = roleIdentifier } } });
            return mockRegisterService;
        }

        public static Mock<IAltinnRegisterService> SetupMainUnitsLookup(this Mock<IAltinnRegisterService> mockRegisterService, string subUnitOrgNo, string mainUnitOrgNo, Guid mainUnitPartyUuid)
        {
            mockRegisterService
                .Setup(s => s.LookUpMainUnits(It.Is<string>(val => val.Contains(subUnitOrgNo)), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<MainUnitItem> { new MainUnitItem { OrganizationIdentifier = mainUnitOrgNo, PartyUuid = mainUnitPartyUuid } });
            return mockRegisterService;
        }

        public static Mock<IAltinnRegisterService> SetupEmptyMainUnitsLookup(this Mock<IAltinnRegisterService> mockRegisterService, string orgNO)
        {
            mockRegisterService
                .Setup(s => s.LookUpMainUnits(It.Is<string>(val => val.Contains(orgNO)), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<MainUnitItem>());
            return mockRegisterService;
        }

        public static Mock<IAltinnRegisterService> SetupPartyByIdLookup(this Mock<IAltinnRegisterService> mockRegisterService, string orgNoMatch, Guid partyUuid)
        {
            mockRegisterService
                .Setup(s => s.LookUpPartyById(It.Is<string>(val => val.Contains(orgNoMatch)), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Party { PartyUuid = partyUuid, OrgNumber = orgNoMatch });
            return mockRegisterService;
        }
    }
}
