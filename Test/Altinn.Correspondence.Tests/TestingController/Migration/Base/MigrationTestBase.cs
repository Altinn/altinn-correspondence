using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Tests.Helpers;
using System.Text.Json;
using System.Web;

namespace Altinn.Correspondence.Tests.TestingController.Migration.Base;
public class MigrationTestBase
{
    internal readonly CustomWebApplicationFactory _factory;
    internal readonly HttpClient _migrationClient;
    internal readonly HttpClient _legacyClient;
    internal readonly HttpClient _recipientClient;
    public readonly string _partyIdClaim = "urn:altinn:partyid";
    public readonly int _digdirPartyId = 50952483;
    public readonly int _testUserPartyId = 100;
    public readonly Guid _testUserPartyUuId = new Guid("358C48B4-74A7-461F-A86F-48801DEEC920");
    public readonly string _delegatedUserName = "Delegert test bruker";
    
    internal readonly JsonSerializerOptions _responseSerializerOptions;

    public MigrationTestBase(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _migrationClient = _factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.MigrateScope));
        _legacyClient = _factory.CreateClientWithAddedClaims(
                ("scope", AuthorizationConstants.LegacyScope),
                (_partyIdClaim, _digdirPartyId.ToString()));
        _recipientClient = _factory.CreateClientWithAddedClaims(
            ("notSender", "true"),
            ("scope", AuthorizationConstants.RecipientScope)
        );
        _responseSerializerOptions = new JsonSerializerOptions(new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        });
        _responseSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    }

    internal string GetAttachmentCommand(MigrateInitializeAttachmentExt attachment)
    {
        return $"correspondence/api/v1/migration/attachment" +
            $"?resourceId={HttpUtility.UrlEncode(attachment.ResourceId)}" +
            $"&senderPartyUuid={HttpUtility.UrlEncode(attachment.SenderPartyUuid.ToString())}" +
            $"&sendersReference={HttpUtility.UrlEncode(attachment.SendersReference)}" +
            $"&displayName={HttpUtility.UrlEncode(attachment.DisplayName)}" +
            $"&isEncrypted={HttpUtility.UrlEncode(attachment.IsEncrypted.ToString())}" +
            $"&fileName={HttpUtility.UrlEncode(attachment.FileName)}" +
            $"&sender={HttpUtility.UrlEncode(attachment.Sender)}" +
            (attachment.Altinn2AttachmentId == null ? "" :
            $"&altinn2AttachmentId={HttpUtility.UrlEncode(attachment.Altinn2AttachmentId?.ToString() ?? "")}");
    }
}
