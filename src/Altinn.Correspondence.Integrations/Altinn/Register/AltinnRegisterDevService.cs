using System.Text.Json;
using System.Text.RegularExpressions;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Register;
using Altinn.Correspondence.Core.Services;
using Altinn.Register.Contracts;

namespace Altinn.Correspondence.Integrations.Altinn.Register;

public class AltinnRegisterDevService : IAltinnRegisterService
{
    private const string _identificationIDPattern = @"^(?:\d{11}|\d{9}|0192:\d{9})$";
    private static readonly Regex IdentificationIDRegex = new(_identificationIDPattern);

    public const int DigdirPartyId = 50952483;
    public static readonly Guid DigdirPartyUuid = new("36E2BCC6-D5B8-4399-AA90-4AFEB2D1A0BF");
    public const string DigdirOrgNumber = "991825827";

    public const int DelegatedUserPartyId = 100;
    public static readonly Guid DelegatedUserPartyUuid = new("358C48B4-74A7-461F-A86F-48801DEEC920");
    public const string DelegatedUserSsn = "10108000398";

    public const int SecondUserPartyId = 200;
    public static readonly Guid SecondUserPartyUuid = new("AE985685-5D8F-45E0-AE00-240F5F5C60C5");
    public const string SecondUserSsn = "13076800124";

    public const int SiUserPartyId = 300;
    public static readonly Guid SiUserPartyUuid = new("11111111-2222-3333-4444-555555555555");

    public const int LegacySiUserPartyId = 301;
    public static readonly Guid LegacySiUserPartyUuid = new("22222222-3333-4444-5555-666666666666");

    public Task<Party?> LookUpPartyById(string identificationId, CancellationToken cancellationToken)
    {
        if (identificationId.IsIdPortenEmailUrn())
        {
            return Task.FromResult<Party?>(BuildIdPortenEmailUser(identificationId.WithoutPrefix()));
        }

        if (identificationId.IsLegacySelfIdentifiedUrn())
        {
            return Task.FromResult<Party?>(BuildLegacySelfIdentifiedUser(identificationId.WithoutPrefix()));
        }

        var clean = identificationId.WithoutPrefix();

        if (Guid.TryParse(clean, out var uuid))
        {
            return Task.FromResult(LookupByUuid(uuid));
        }

        if (identificationId.IsPartyId() && int.TryParse(clean, out var partyId))
        {
            return Task.FromResult(LookupByPartyId(partyId));
        }

        if (IdentificationIDRegex.IsMatch(clean))
        {
            return Task.FromResult<Party?>(BuildDigdirOrganization());
        }

        return Task.FromResult<Party?>(null);
    }

    public Task<List<Party>?> LookUpPartiesByIds(List<string> identificationIds, CancellationToken cancellationToken)
    {
        var parties = new List<Party>();
        foreach (var id in identificationIds)
        {
            if (IdentificationIDRegex.IsMatch(id.WithoutPrefix()))
            {
                parties.Add(BuildDigdirOrganization());
            }
        }
        return Task.FromResult<List<Party>?>(parties);
    }

    public Task<List<RoleItem>> LookUpPartyRoles(string partyUuid, CancellationToken cancellationToken)
    {
        var roles = new List<RoleItem>();
        var toPartyUuid = Guid.NewGuid();
        if (partyUuid == DigdirPartyUuid.ToString())
        {
            roles.Add(new RoleItem
            {
                Role = new RoleDescriptor { Source = "ccr", Identifier = "daglig-leder", Urn = "urn:altinn:external-role:ccr:daglig-leder" },
                From = new RoleParty { PartyUuid = DigdirPartyUuid, Urn = $"urn:altinn:party:uuid:{DigdirPartyUuid}" },
                To = new RoleParty { PartyUuid = toPartyUuid, Urn = $"urn:altinn:party:uuid:{toPartyUuid}" },
            });
        }
        return Task.FromResult(roles);
    }

    public Task<List<MainUnitItem>> LookUpMainUnits(string urn, CancellationToken cancellationToken)
    {
        var items = new List<MainUnitItem>
        {
            new()
            {
                PartyType = "organization",
                OrganizationIdentifier = DigdirOrgNumber,
                PartyUuid = DigdirPartyUuid,
                VersionId = 1,
                Urn = $"urn:altinn:party:uuid:{DigdirPartyUuid}",
                PartyId = DigdirPartyId,
                DisplayName = "Digitaliseringsdirektoratet"
            }
        };
        return Task.FromResult(items);
    }

    private static Party? LookupByUuid(Guid uuid)
    {
        if (uuid == DigdirPartyUuid) return BuildDigdirOrganization();
        if (uuid == DelegatedUserPartyUuid) return BuildPerson(DelegatedUserPartyId, DelegatedUserPartyUuid, DelegatedUserSsn, "Delegert test bruker");
        if (uuid == SecondUserPartyUuid) return BuildPerson(SecondUserPartyId, SecondUserPartyUuid, SecondUserSsn, "Annen test bruker");
        if (uuid == SiUserPartyUuid) return BuildIdPortenEmailUser("si-user@example.com");
        if (uuid == LegacySiUserPartyUuid) return BuildLegacySelfIdentifiedUser("si-user");
        return null;
    }

    private static Party? LookupByPartyId(int partyId) => partyId switch
    {
        DigdirPartyId => BuildDigdirOrganization(),
        DelegatedUserPartyId => BuildPerson(DelegatedUserPartyId, DelegatedUserPartyUuid, DelegatedUserSsn, "Delegert test bruker"),
        SecondUserPartyId => BuildPerson(SecondUserPartyId, SecondUserPartyUuid, SecondUserSsn, "Annen test bruker"),
        SiUserPartyId => BuildIdPortenEmailUser("si-user@example.com"),
        LegacySiUserPartyId => BuildLegacySelfIdentifiedUser("si-user"),
        _ => null,
    };

    internal static Party BuildDigdirOrganization() => Deserialize($$"""
        {
          "partyUuid": "{{DigdirPartyUuid}}",
          "partyType": "organization",
          "partyId": {{DigdirPartyId}},
          "displayName": "Digitaliseringsdirektoratet",
          "organizationIdentifier": "{{DigdirOrgNumber}}",
          "unitType": "ORGL",
          "isDeleted": false,
          "versionId": 1,
          "externalUrn": "{{UrnConstants.OrganizationNumberAttribute}}:{{DigdirOrgNumber}}"
        }
        """);

    private static Party BuildPerson(int partyId, Guid uuid, string ssn, string displayName) => Deserialize($$"""
        {
          "partyUuid": "{{uuid}}",
          "partyType": "person",
          "partyId": {{partyId}},
          "displayName": "{{displayName}}",
          "personIdentifier": "{{ssn}}",
          "isDeleted": false,
          "versionId": 1,
          "externalUrn": "{{UrnConstants.PersonIdAttribute}}:{{ssn}}"
        }
        """);

    private static Party BuildIdPortenEmailUser(string email) => Deserialize($$"""
        {
          "partyUuid": "{{SiUserPartyUuid}}",
          "partyType": "self-identified-user",
          "partyId": {{SiUserPartyId}},
          "displayName": "SI user with email {{email}}",
          "isDeleted": false,
          "versionId": 1,
          "user": { "userId": 999, "username": "{{email}}", "userIds": [999] },
          "externalUrn": "{{UrnConstants.PersonIdPortenEmailAttribute}}:{{email}}"
        }
        """);

    private static Party BuildLegacySelfIdentifiedUser(string username) => Deserialize($$"""
        {
          "partyUuid": "{{LegacySiUserPartyUuid}}",
          "partyType": "self-identified-user",
          "partyId": {{LegacySiUserPartyId}},
          "displayName": "Legacy SI user {{username}}",
          "isDeleted": false,
          "versionId": 1,
          "user": { "userId": 998, "username": "{{username}}", "userIds": [998] },
          "externalUrn": "{{UrnConstants.PersonLegacySelfIdentifiedAttribute}}:{{username}}"
        }
        """);

    private static Party Deserialize(string json)
        => JsonSerializer.Deserialize<Party>(json, JsonSerializerOptions.Web)!;
}
