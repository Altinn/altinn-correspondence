using System.Security.Claims;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Tests.Helpers;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

public class UploadFailsWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly List<Claim> _claims = new List<Claim>
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
        new Claim("urn:altinn:partyid", "1"),
        new Claim("scope", "altinn:correspondence.write")
    };
    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        // Overwrite registrations from Program.cs
        builder.ConfigureTestServices((services) =>
        {
            var altinnAuthorizationService = new Mock<IAltinnAuthorizationService>();
            altinnAuthorizationService.Setup(x => x.CheckUserAccess(It.IsAny<string>(), It.IsAny<List<ResourceAccessLevel>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            services.AddSingleton(altinnAuthorizationService.Object);
            services.AddSingleton<IPolicyEvaluator, MockPolicyEvaluator>(_ => 
            {
                return new MockPolicyEvaluator(_claims);
            });
            var storageMock = new Mock<IStorageRepository>();
            storageMock.Setup(x => x.UploadAttachment(It.IsAny<AttachmentEntity>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>())).Callback(() =>
            {
                Thread.Sleep(5000);
            });
            services.AddScoped(_ => storageMock.Object);
        });
    }

    public HttpClient CreateClientInternal()
    {
        var client = CreateClient();
        return client;
    }
}