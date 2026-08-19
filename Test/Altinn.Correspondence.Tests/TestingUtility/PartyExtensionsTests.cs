using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Extensions;
using Altinn.Register.Contracts;
using System.Text.Json;

namespace Altinn.Correspondence.Tests.TestingUtility
{
    public class PartyExtensionsTests
    {
        [Fact]
        public void GetExternalUrn_ReturnsPersonUrn_WhenExternalUrnIsNull()
        {
            // Arrange
            const string personIdentifier = "08900499559";
            var partyString = $$"""
            {
              "partyType": "person",
              "personIdentifier": "{{personIdentifier}}",
              "partyUuid": "cd3cd418-6498-4763-8668-cd196989fbf4",
              "versionId": 12345678,
              "urn": "urn:altinn:party:uuid:cd3cd418-6498-4763-8668-cd196989fbf4",
              "externalUrn": null,
              "partyId": 87654321,
              "displayName": "OLA NORDMANN",
              "user": {
                "userId": 88888888,
                "username": null,
                "userIds": [
                  88888888
                ],
                "usernames": []
              }
            }
            """;

            // Act
            var party = JsonSerializer.Deserialize<Party>(partyString, JsonSerializerOptions.Web);
            var externalUrn = party!.GetExternalUrn();

            // Assert
            Assert.Equal($"{UrnConstants.PersonIdAttribute}:{personIdentifier}", externalUrn);
        }

        [Fact]
        public void GetExternalUrn_ReturnsOrganizationUrn_FromRegisterPayload()
        {
            // Arrange
            const string organizationIdentifier = "991825827";
            var expectedUrn = $"{UrnConstants.OrganizationNumberAttribute}:{organizationIdentifier}";
            var partyString = $$"""
            {
              "partyType": "organization",
              "organizationIdentifier": "{{organizationIdentifier}}",
              "partyUuid": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
              "versionId": 1,
              "urn": "urn:altinn:party:uuid:aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
              "externalUrn": "{{expectedUrn}}",
              "partyId": 1000000001,
              "displayName": "TEST ORGANIZATION AS",
              "user": null
            }
            """;

            // Act
            var party = JsonSerializer.Deserialize<Party>(partyString, JsonSerializerOptions.Web);
            var externalUrn = party!.GetExternalUrn();

            // Assert
            Assert.Equal(expectedUrn, externalUrn);
        }

        [Fact]
        public void GetExternalUrn_ReturnsPersonUrn_FromRegisterPayloadWithUser()
        {
            // Arrange
            const string personIdentifier = "08900499559";
            var expectedUrn = $"{UrnConstants.PersonIdAttribute}:{personIdentifier}";
            var partyString = $$"""
            {
              "partyType": "person",
              "personIdentifier": "{{personIdentifier}}",
              "partyUuid": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
              "versionId": 1,
              "urn": "urn:altinn:party:uuid:bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
              "externalUrn": "{{expectedUrn}}",
              "partyId": 2000000002,
              "displayName": "OLA NORDMANN",
              "user": {
                "userId": 3000003,
                "username": "testuser",
                "userIds": [
                  3000003
                ],
                "usernames": [
                  "testuser"
                ]
              }
            }
            """;

            // Act
            var party = JsonSerializer.Deserialize<Party>(partyString, JsonSerializerOptions.Web);
            var externalUrn = party!.GetExternalUrn();

            // Assert
            Assert.Equal(expectedUrn, externalUrn);
        }

        [Fact]
        public void GetExternalUrn_ReturnsOrganizationUrn_WhenExternalUrnIsNull()
        {
            // Arrange
            const string organizationIdentifier = "991825827";
            var expectedUrn = $"{UrnConstants.OrganizationNumberAttribute}:{organizationIdentifier}";
            var partyString = $$"""
            {
              "partyType": "organization",
              "organizationIdentifier": "{{organizationIdentifier}}",
              "partyUuid": "dddddddd-dddd-dddd-dddd-dddddddddddd",
              "versionId": 1,
              "urn": "urn:altinn:party:uuid:dddddddd-dddd-dddd-dddd-dddddddddddd",
              "externalUrn": null,
              "partyId": 4000004,
              "displayName": "FALLBACK ORG AS",
              "user": null
            }
            """;

            // Act
            var party = JsonSerializer.Deserialize<Party>(partyString, JsonSerializerOptions.Web);
            var externalUrn = party!.GetExternalUrn();

            // Assert
            Assert.Equal(expectedUrn, externalUrn);
        }

        [Fact]
        public void GetExternalUrn_ReturnsLowercasedEmailUrn_WhenExternalUrnIsNull()
        {
            // Arrange — ID-Porten email usernames should be lowercased in the fallback URN
            const string username = "Test.User@Example.COM";
            var expectedUrn = $"{UrnConstants.PersonIdPortenEmailAttribute}:{username.ToLowerInvariant()}";
            var partyString = $$"""
            {
              "partyType": "self-identified-user",
              "partyUuid": "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee",
              "versionId": 1,
              "urn": "urn:altinn:party:uuid:eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee",
              "externalUrn": null,
              "partyId": 5000005,
              "displayName": "SI EMAIL USER",
              "user": { "userId": 7000007, "username": "{{username}}", "userIds": [7000007] }
            }
            """;

            // Act
            var party = JsonSerializer.Deserialize<Party>(partyString, JsonSerializerOptions.Web);
            var externalUrn = party!.GetExternalUrn();

            // Assert
            Assert.Equal(expectedUrn, externalUrn);
        }

        [Fact]
        public void GetExternalUrn_ReturnsLegacyUrn_PreservingCase_WhenExternalUrnIsNull()
        {
            // Arrange — legacy (non-email) usernames are preserved as-is
            const string username = "LegacyUser42";
            var expectedUrn = $"{UrnConstants.PersonLegacySelfIdentifiedAttribute}:{username}";
            var partyString = $$"""
            {
              "partyType": "self-identified-user",
              "partyUuid": "ffffffff-ffff-ffff-ffff-ffffffffffff",
              "versionId": 1,
              "urn": "urn:altinn:party:uuid:ffffffff-ffff-ffff-ffff-ffffffffffff",
              "externalUrn": null,
              "partyId": 6000006,
              "displayName": "SI LEGACY USER",
              "user": { "userId": 8000008, "username": "{{username}}", "userIds": [8000008] }
            }
            """;

            // Act
            var party = JsonSerializer.Deserialize<Party>(partyString, JsonSerializerOptions.Web);
            var externalUrn = party!.GetExternalUrn();

            // Assert
            Assert.Equal(expectedUrn, externalUrn);
        }

        [Fact]
        public void GetExternalUrn_ReturnsSystemUserUrn_FromRegisterPayload()
        {
            // Arrange
            const string partyUuid = "cccccccc-cccc-cccc-cccc-cccccccccccc";
            var expectedUrn = $"{UrnConstants.SystemUser}:uuid:{partyUuid}";
            var partyString = $$"""
            {
              "partyType": "system-user",
              "partyUuid": "{{partyUuid}}",
              "versionId": 1,
              "urn": "urn:altinn:party:uuid:{{partyUuid}}",
              "externalUrn": "{{expectedUrn}}",
              "partyId": null,
              "displayName": "TestSystem-000000000_test-broker",
              "user": null
            }
            """;

            // Act
            var party = JsonSerializer.Deserialize<Party>(partyString, JsonSerializerOptions.Web);
            var externalUrn = party!.GetExternalUrn();

            // Assert
            Assert.Equal(expectedUrn, externalUrn);
        }

        /// <summary>
        /// Regression guard for the Altinn.Register.Contracts 1.7.0 upgrade: <c>externalUrn</c> moved from
        /// <c>JsonExtensionData</c> to a typed property. The username here derives to the legacy URN, so the
        /// test only passes if the API-provided value is actually read rather than silently falling through
        /// to the <see cref="Party"/>-derived construction.
        /// </summary>
        [Fact]
        public void GetExternalUrn_PrefersApiExternalUrn_OverDerivedUrn()
        {
            // Arrange
            const string username = "LegacyUser";
            var apiUrn = $"{UrnConstants.PersonIdPortenEmailAttribute}:ola@example.com";
            var derivedUrn = $"{UrnConstants.PersonLegacySelfIdentifiedAttribute}:{username}";
            var partyString = $$"""
            {
              "partyType": "self-identified-user",
              "partyUuid": "dddddddd-dddd-dddd-dddd-dddddddddddd",
              "versionId": 1,
              "urn": "urn:altinn:party:uuid:dddddddd-dddd-dddd-dddd-dddddddddddd",
              "externalUrn": "{{apiUrn}}",
              "partyId": 1000000002,
              "displayName": "Test SI User",
              "user": {
                "userId": 77777777,
                "username": "{{username}}",
                "userIds": [
                  77777777
                ]
              }
            }
            """;

            // Act
            var party = JsonSerializer.Deserialize<Party>(partyString, JsonSerializerOptions.Web);
            var externalUrn = party!.GetExternalUrn();

            // Assert
            Assert.Equal(apiUrn, externalUrn);
            Assert.NotEqual(derivedUrn, externalUrn);
        }

        /// <summary>
        /// The typed <c>ExternalUrn</c> is wrapped in <c>NonExhaustive</c>, so URN schemes the package does
        /// not know must still round-trip verbatim instead of being dropped.
        /// </summary>
        [Fact]
        public void GetExternalUrn_PreservesUrnScheme_UnknownToContractsPackage()
        {
            // Arrange
            const string unknownSchemeUrn = "urn:altinn:some-future-scheme:abc123";
            var partyString = $$"""
            {
              "partyType": "system-user",
              "partyUuid": "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee",
              "versionId": 1,
              "urn": "urn:altinn:party:uuid:eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee",
              "externalUrn": "{{unknownSchemeUrn}}",
              "partyId": null,
              "displayName": "Future Party",
              "user": null
            }
            """;

            // Act
            var party = JsonSerializer.Deserialize<Party>(partyString, JsonSerializerOptions.Web);
            var externalUrn = party!.GetExternalUrn();

            // Assert
            Assert.Equal(unknownSchemeUrn, externalUrn);
        }
    }
}
