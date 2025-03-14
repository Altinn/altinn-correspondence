using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Altinn.AccessManagement;
using Altinn.Correspondence.Integrations.Altinn.Events;
using Altinn.Correspondence.Integrations.Altinn.Notifications;
using Altinn.Correspondence.Integrations.Altinn.Register;
using Altinn.Correspondence.Integrations.Dialogporten;
using Hangfire;
using Hangfire.Common;
using Hangfire.MemoryStorage;
using Hangfire.States;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

namespace Altinn.Correspondence.Tests.Helpers;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
    internal Mock<IBackgroundJobClient>? HangfireBackgroundJobClient;
    public const string ReservedSsn = "08900499559";
    public Action<IServiceCollection>? CustomServices;
    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseConfiguration(new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json")
            .Build());

        // Overwrite registrations from Program.cs
        builder.ConfigureTestServices((services) =>
        {
            services.AddHangfire(config =>
                           config.UseMemoryStorage()
                       );
            HangfireBackgroundJobClient = new Mock<IBackgroundJobClient>();
            HangfireBackgroundJobClient.Setup(x => x.Create(
                It.IsAny<Job>(), 
                It.IsAny<IState>())).Returns("1"); 
            services.AddSingleton(HangfireBackgroundJobClient.Object);
            services.AddScoped<IEventBus, ConsoleLogEventBus>();
            services.AddScoped<IAltinnNotificationService, AltinnDevNotificationService>();
            services.AddScoped<IDialogportenService, DialogportenDevService>();
            services.OverrideAuthentication();
            services.OverrideAuthorization();
            services.OverrideAltinnAuthorization();
            services.AddScoped<IAltinnRegisterService, AltinnRegisterDevService>();
            services.AddScoped<IAltinnAccessManagementService, AltinnAccessManagementDevService>();
            var mockContactReservationRegistryService = new Mock<IContactReservationRegistryService>();
            mockContactReservationRegistryService.Setup(x => x.GetReservedRecipients(It.Is<List<string>>(recipients => recipients.Contains(ReservedSsn)))).ReturnsAsync([ReservedSsn]);
            mockContactReservationRegistryService.Setup(x => x.GetReservedRecipients(It.Is<List<string>>(recipients => !recipients.Contains(ReservedSsn)))).ReturnsAsync([]);
            services.AddScoped(_ => mockContactReservationRegistryService.Object);
            var resourceRegistryService = new Mock<IResourceRegistryService>();
            resourceRegistryService.Setup(x => x.GetServiceOwnerOfResource(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("");
            resourceRegistryService.Setup(x => x.GetResourceType(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("CorrespondenceService");
            services.AddScoped(_ => resourceRegistryService.Object);
        });
        if (CustomServices is not null)
        {
            builder.ConfigureServices(CustomServices);
        }
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
        var client = CreateClient();
        var claimsData = defaultClaims.Select(c => new Dictionary<string, string> 
        { 
            { "Type", c.Type }, 
            { "Value", c.Value } 
        }).ToList();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        client.DefaultRequestHeaders.Add("X-Custom-Claims", JsonSerializer.Serialize(claimsData));
        return client;
    }

    public HttpClient CreateClientWithDialogportenClaims(string? issuer, params (string type, string value)[] claims)
    {
        var defaultClaims = new List<Claim>
        {
            new Claim("jti", "fdf63f48-f470-49f8-bda0-0e8f7b4dbcb8", null, issuer),
            new Claim("c", "urn:altinn:person:identifier-no:04825999731", null, issuer),
            new Claim("l", "3", null, issuer),
            new Claim("p", "urn:altinn:organization:identifier-no:310654302", null, issuer),
            new Claim("s", "urn:altinn:resource:dagl-correspondence", null, issuer),
            new Claim("i", "01926bb3-5b36-777f-bf9a-73bf5a7baa2e", null, issuer),
            new Claim("a", "read", null, issuer),
            new Claim("iss", "https://platform.tt02.altinn.no/dialogporten/api/v1", null, issuer),
            new Claim("iat", "1728457448", null, issuer),
            new Claim("nbf", "1728457448", null, issuer),
            new Claim("exp", "1728458048", null, issuer)
        };
        foreach (var (type, value) in claims)
        {
            defaultClaims.RemoveAll(c => c.Type == type);
            defaultClaims.Add(new Claim(type, value));
        }

        var client = CreateClient();
        var claimsData = defaultClaims.Select(c => new Dictionary<string, string>
        {
            { "Type", c.Type },
            { "Value", c.Value }
        }).ToList();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        client.DefaultRequestHeaders.Add("X-Custom-Claims", JsonSerializer.Serialize(claimsData));
        return client;
    }

    public HttpClient CreateSenderClient() => CreateClientWithAddedClaims(
            ("notRecipient", "true"),
            ("scope", AuthorizationConstants.SenderScope)
        );
}
