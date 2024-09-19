using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Tests.Helpers;
using Azure.Messaging.EventGrid.SystemEvents;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Moq;

public class UploadFailsWebApplicationFactory : WebApplicationFactory<Program>
{
    internal Mock<IBackgroundJobClient>? HangfireBackgroundJobClient;
    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        // Overwrite registrations from Program.cs
        builder.ConfigureTestServices((services) =>
        {
            var altinnAuthorizationService = new Mock<IAltinnAuthorizationService>();
            altinnAuthorizationService.Setup(x => x.CheckUserAccess(It.IsAny<string>(), It.IsAny<List<ResourceAccessLevel>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            services.AddSingleton(altinnAuthorizationService.Object);
            services.AddSingleton<IPolicyEvaluator, MockPolicyEvaluator>();
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