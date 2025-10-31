using Altinn.Correspondence.Application.MigrateCorrespondence;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Helpers;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Altinn.Correspondence.Tests.TestingFeature;

public class MigrateCorrespondenceExternalReferencesTests
{
    [Fact]
    public async Task MakeCorrespondenceAvailable_AddsSingleDialogportenExternalReference()
    {
        // Arrange
        var mockDialogportenService = new Mock<Core.Services.IDialogportenService>();
        mockDialogportenService
            .Setup(x => x.CreateCorrespondenceDialogForMigratedCorrespondence(
                It.IsAny<Guid>(),
                It.IsAny<CorrespondenceEntity>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .ReturnsAsync("dialog-123");

        var mockBackgroundJobClient = new Mock<IBackgroundJobClient>();
        mockBackgroundJobClient
            .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns(() => Guid.NewGuid().ToString());

        using var factory = new UnitWebApplicationFactory((IServiceCollection services) =>
        {
            services.AddSingleton(mockDialogportenService.Object);
            services.AddSingleton(mockBackgroundJobClient.Object);
        });

        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICorrespondenceRepository>();
        var handler = scope.ServiceProvider.GetRequiredService<MigrateCorrespondenceHandler>();

        var entity = new CorrespondenceEntityBuilder()
            .WithStatus(CorrespondenceStatus.Initialized)
            .Build();

        var created = await repository.CreateCorrespondence(entity, CancellationToken.None);

        // Act
        var dialogId = await handler.MakeCorrespondenceAvailableInDialogportenAndApi(created.Id, CancellationToken.None);

        // Assert
        Assert.Equal("dialog-123", dialogId);

        var persisted = await repository.GetCorrespondenceById(created.Id, true, true, false, CancellationToken.None);
        Assert.NotNull(persisted);

        var dialogRefs = persisted!.ExternalReferences
            .Where(r => r.ReferenceType == ReferenceType.DialogportenDialogId)
            .ToList();

        Assert.Single(dialogRefs);
        Assert.Equal("dialog-123", dialogRefs[0].ReferenceValue);
    }
}


