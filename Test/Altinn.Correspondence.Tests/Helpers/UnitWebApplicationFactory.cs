﻿using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Altinn.AccessManagement;
using Altinn.Correspondence.Integrations.Altinn.Authorization;
using Altinn.Correspondence.Integrations.Altinn.Events;
using Altinn.Correspondence.Integrations.Altinn.Notifications;
using Altinn.Correspondence.Integrations.Altinn.Register;
using Altinn.Correspondence.Integrations.Dialogporten;
using Altinn.Correspondence.Repositories;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Security.Claims;

namespace Altinn.Correspondence.Tests.Helpers
{
    internal class UnitWebApplicationFactory : WebApplicationFactory<Program>
    {
        private Action<IServiceCollection>? _customServices;

        public UnitWebApplicationFactory(Action<IServiceCollection> customServices)
        {
            _customServices = customServices;
        }

        protected override void ConfigureWebHost(
            IWebHostBuilder builder)
        {
            // Overwrite registrations from Program.cs
            builder.ConfigureTestServices((services) =>
            {
                services.AddHangfire(config =>
                               config.UseMemoryStorage()
                           );
                services.AddScoped<IEventBus, ConsoleLogEventBus>();
                services.AddScoped<IAltinnNotificationService, AltinnDevNotificationService>();
                services.AddScoped<IDialogportenService, DialogportenDevService>();
                services.AddScoped<IAltinnAuthorizationService, AltinnAuthorizationDevService>();
                services.AddScoped<IAltinnRegisterService, AltinnRegisterDevService>();
                services.AddScoped<IAltinnAccessManagementService, AltinnAccessManagementDevService>();
                var resourceRightsService = new Mock<IResourceRightsService>();
                resourceRightsService.Setup(x => x.GetServiceOwnerOfResource(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("");
                services.AddScoped(_ => resourceRightsService.Object);
                if (_customServices is not null)
                    _customServices(services);
            });
        }
        public HttpClient CreateClientWithAddedClaims(params (string type, string value)[] claims)
        {
            var defaultClaims = new List<Claim>
        {
            new Claim("urn:altinn:authlevel", "3"),
            new Claim("client_amr", "virksomhetssertifikat"),
            new Claim("pid", "11015699332"),
            new Claim("token_type", "Bearer"),
            new Claim("client_id", "5b7b5418-1196-4539-bd1b-5f7c6fdf5963"),
            new Claim("http://schemas.microsoft.com/claims/authnclassreference", "Level3"),
            new Claim("exp", "1721895043"),
            new Claim("iat", "1721893243"),
            new Claim("client_orgno", "991825827"),
            new Claim("consumer", "{\"authority\":\"iso6523-actorid-upis\",\"ID\":\"0192:991825827\"}"),
            new Claim("iss", "https://platform.tt02.altinn.no/authentication/api/v1/openid/"),
            new Claim("actual_iss", "mock"),
            new Claim("nbf", "1721893243"),
            new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", "1"),
            new Claim("urn:altinn:userid", "1"),
            new Claim("urn:altinn:partyid", "1")
        };
            var claimsWithDuplicatesAllowed = new List<string> { "scope" };
            foreach (var (type, value) in claims)
            {
                if (claimsWithDuplicatesAllowed.Contains(type))
                {
                    defaultClaims.Add(new Claim(type, value));
                }
                else
                {
                    defaultClaims.RemoveAll(c => c.Type == type);
                    defaultClaims.Add(new Claim(type, value));
                }
            }
            // Clone the current factory and set the specific claims for this instance
            var clientFactory = WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<IPolicyEvaluator>(provider =>
                    {
                        return new MockPolicyEvaluator(defaultClaims);
                    });
                });
            });
            return clientFactory.CreateClient();
        }
    }
}
