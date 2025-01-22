using Altinn.Correspondence.Common.Constants;
using System.Text.Json;

namespace Altinn.Correspondence.Tests.TestingController.Attachment.Base
{
    public class AttachmentTestBase : IClassFixture<CustomWebApplicationFactory>
    {
        public readonly CustomWebApplicationFactory _factory;
        public readonly HttpClient _senderClient;
        public readonly HttpClient _recipientClient;
        public readonly HttpClient _wrongSenderClient;
        public readonly JsonSerializerOptions _responseSerializerOptions;

        public AttachmentTestBase(CustomWebApplicationFactory factory)
        {
            //_factory = factory;
            _factory = new CustomWebApplicationFactory();
            _senderClient = factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.SenderScope));
            _recipientClient = factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.RecipientScope));
            _wrongSenderClient = factory.CreateClientWithAddedClaims(
                ("scope", AuthorizationConstants.SenderScope), 
                ("notSender", "true")
            );
            _responseSerializerOptions = new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true
            };
            _responseSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        }
    }
}
