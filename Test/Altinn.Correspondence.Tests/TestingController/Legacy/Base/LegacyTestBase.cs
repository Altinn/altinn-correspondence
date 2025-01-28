using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Tests.Helpers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.Tests.TestingController.Legacy.Base
{
    public class LegacyTestBase
    {
        public readonly CustomWebApplicationFactory _factory;
        public readonly JsonSerializerOptions _serializerOptions;
        public readonly HttpClient _legacyClient;
        public readonly HttpClient _senderClient;
        public readonly string _partyIdClaim = "urn:altinn:partyid";
        public readonly int _digdirPartyId = 50952483;

        public LegacyTestBase(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _serializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };

            _senderClient = _factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.SenderScope));
            _legacyClient = _factory.CreateClientWithAddedClaims(
                ("scope", AuthorizationConstants.LegacyScope),
                (_partyIdClaim, _digdirPartyId.ToString()));
        }
    }
}
