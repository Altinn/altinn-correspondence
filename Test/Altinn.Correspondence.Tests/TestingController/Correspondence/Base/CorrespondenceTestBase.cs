using Altinn.Correspondence.Application.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Altinn.Correspondence.Tests.TestingController.Correspondence.Base
{
    public class CorrespondenceTestBase : IClassFixture<CustomWebApplicationFactory>
    {
        internal readonly CustomWebApplicationFactory _factory;
        internal readonly HttpClient _senderClient;
        internal readonly HttpClient _recipientClient;
        internal readonly JsonSerializerOptions _responseSerializerOptions;

        public CorrespondenceTestBase(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _senderClient = _factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.SenderScope));
            _recipientClient = _factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.RecipientScope));
            _responseSerializerOptions = new JsonSerializerOptions(new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true
            });
            _responseSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        }
    }
}
