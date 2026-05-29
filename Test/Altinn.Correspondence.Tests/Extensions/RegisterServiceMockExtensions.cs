using System.Text.Json;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Models.Register;
using Altinn.Correspondence.Core.Services;
using Altinn.Register.Contracts;
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
                .ReturnsAsync(BuildOrganization(partyUuid, orgNoMatch));
            return mockRegisterService;
        }

        /// <summary>
        /// Build a test <see cref="Organization"/> by deserialization. Required-member init constructs are
        /// noisy for the polymorphic v2 model.
        /// </summary>
        public static Party BuildOrganization(Guid partyUuid, string organizationIdentifier, uint? partyId = null, string? displayName = null)
        {
            var json = $$"""
                {
                  "partyUuid": "{{partyUuid}}",
                  "partyType": "organization",
                  "partyId": {{partyId ?? 0u}},
                  "displayName": "{{displayName ?? "Test Organization"}}",
                  "organizationIdentifier": "{{organizationIdentifier}}",
                  "isDeleted": false,
                  "versionId": 1,
                  "externalUrn": "urn:altinn:organization:identifier-no:{{organizationIdentifier}}"
                }
                """;
            return JsonSerializer.Deserialize<Party>(json, JsonSerializerOptions.Web)!;
        }

        public static Party BuildPerson(Guid partyUuid, string personIdentifier, uint? partyId = null, string? displayName = null)
        {
            var json = $$"""
                {
                  "partyUuid": "{{partyUuid}}",
                  "partyType": "person",
                  "partyId": {{partyId ?? 0u}},
                  "displayName": "{{displayName ?? "Test Person"}}",
                  "personIdentifier": "{{personIdentifier}}",
                  "isDeleted": false,
                  "versionId": 1,
                  "externalUrn": "urn:altinn:person:identifier-no:{{personIdentifier}}"
                }
                """;
            return JsonSerializer.Deserialize<Party>(json, JsonSerializerOptions.Web)!;
        }

        public static Party BuildSelfIdentifiedUser(Guid partyUuid, string username, uint userId = 999, uint? partyId = null, string? displayName = null)
        {
            var externalUrnPrefix = username.Contains('@')
                ? UrnConstants.PersonIdPortenEmailAttribute
                : UrnConstants.PersonLegacySelfIdentifiedAttribute;
            var json = $$"""
                {
                  "partyUuid": "{{partyUuid}}",
                  "partyType": "self-identified-user",
                  "partyId": {{partyId ?? 0u}},
                  "displayName": "{{displayName ?? "Test SI User"}}",
                  "isDeleted": false,
                  "versionId": 1,
                  "user": { "userId": {{userId}}, "username": "{{username}}", "userIds": [{{userId}}] },
                  "externalUrn": "{{externalUrnPrefix}}:{{username}}"
                }
                """;
            return JsonSerializer.Deserialize<Party>(json, JsonSerializerOptions.Web)!;
        }
    }
}
