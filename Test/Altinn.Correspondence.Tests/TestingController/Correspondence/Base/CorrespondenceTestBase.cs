using Altinn.Correspondence.Common.Constants;
using System.Text.Json;

namespace Altinn.Correspondence.Tests.TestingController.Correspondence.Base
{
    public class CorrespondenceTestBase : IClassFixture<CustomWebApplicationFactory>, IDisposable
    {
        internal readonly CustomWebApplicationFactory _factory;
        internal readonly HttpClient _senderClient;
        internal readonly HttpClient _recipientClient;
        internal readonly JsonSerializerOptions _responseSerializerOptions;

        public CorrespondenceTestBase(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _senderClient = _factory.CreateClientWithAddedClaims(
                ("notRecipient", "true"),
                ("scope", AuthorizationConstants.SenderScope)
            );
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

        public void Dispose()
        {
            _factory?.Dispose();
        }
    }
}
