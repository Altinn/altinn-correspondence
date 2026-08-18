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
    }
}
