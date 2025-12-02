using Altinn.Correspondence.Application.CleanupConfirmedMigratedCorrespondences;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Hangfire;
using Microsoft.Extensions.Logging;
using Moq;
using Altinn.Correspondence.Tests.Factories;


namespace Altinn.Correspondence.Tests.TestingHandler;

public class CleanupConfirmedMigratedCorrespondencesHandlerTests
{
    [Fact]
    public async Task ExecuteCleanupInBackground_PatchesConfirmedDialogs()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var c1 = new CorrespondenceEntityBuilder()
            .WithCreated(now.UtcDateTime.AddMinutes(-3))
            .WithStatus(CorrespondenceStatus.Published)
            .WithStatus(CorrespondenceStatus.Confirmed)
            .WithDialogId("d1")
            .WithAltinn2CorrespondenceId(1001)
            .Build();
        var c2 = new CorrespondenceEntityBuilder()
            .WithCreated(now.UtcDateTime.AddMinutes(-2))
            .WithStatus(CorrespondenceStatus.Published)
            .WithStatus(CorrespondenceStatus.Confirmed)
            .WithDialogId("d2")
            .WithAltinn2CorrespondenceId(1002)
            .Build();

        var repo = new Mock<ICorrespondenceRepository>();
        repo.Setup(r => r.GetCorrespondencesWithAltinn2IdNotMigratingAndConfirmedStatusUsingCursor(
            It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync([c1, c2]);

        var dialog = new Mock<IDialogportenService>();
        dialog.Setup(d => d.PatchCorrespondenceDialogToConfirmed(c1.Id)).ReturnsAsync(true);
        dialog.Setup(d => d.PatchCorrespondenceDialogToConfirmed(c2.Id)).ReturnsAsync(false);

        var bg = new Mock<IBackgroundJobClient>();
        var logger = new Mock<ILogger<CleanupConfirmedMigratedCorrespondencesHandler>>();
        var handler = new CleanupConfirmedMigratedCorrespondencesHandler(repo.Object, dialog.Object, bg.Object, logger.Object);

        // Act
        await handler.ExecuteCleanupInBackground(50, CancellationToken.None);

        // Assert
        dialog.Verify(d => d.PatchCorrespondenceDialogToConfirmed(c1.Id), Times.Once);
        dialog.Verify(d => d.PatchCorrespondenceDialogToConfirmed(c2.Id), Times.Once);
    }

    [Fact]
    public async Task ExecuteCleanupInBackground_SkipsCorrespondenceWithoutDialogId()
    {
        // Arrange
        var c = new CorrespondenceEntityBuilder()
            .WithStatus(CorrespondenceStatus.Published)
            .WithStatus(CorrespondenceStatus.Confirmed)
            .WithAltinn2CorrespondenceId(2001)
            .Build();

        var repo = new Mock<ICorrespondenceRepository>();
        repo.Setup(r => r.GetCorrespondencesWithAltinn2IdNotMigratingAndConfirmedStatusUsingCursor(
            It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync([c]);

        var dialog = new Mock<IDialogportenService>();
        var bg = new Mock<IBackgroundJobClient>();
        var logger = new Mock<ILogger<CleanupConfirmedMigratedCorrespondencesHandler>>();
        var handler = new CleanupConfirmedMigratedCorrespondencesHandler(repo.Object, dialog.Object, bg.Object, logger.Object);

        // Act
        await handler.ExecuteCleanupInBackground(25, CancellationToken.None);

        // Assert
        dialog.Verify(d => d.PatchCorrespondenceDialogToConfirmed(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteCleanupInBackground_SkipsCorrespondenceNotConfirmed()
    {
        // Arrange
        var c = new CorrespondenceEntityBuilder()
            .WithStatus(CorrespondenceStatus.Published)
            .WithDialogId("d-non-confirmed")
            .WithAltinn2CorrespondenceId(3001)
            .Build();

        var repo = new Mock<ICorrespondenceRepository>();
        repo.Setup(r => r.GetCorrespondencesWithAltinn2IdNotMigratingAndConfirmedStatusUsingCursor(
            It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync([c]);

        var dialog = new Mock<IDialogportenService>();
        var bg = new Mock<IBackgroundJobClient>();
        var logger = new Mock<ILogger<CleanupConfirmedMigratedCorrespondencesHandler>>();
        var handler = new CleanupConfirmedMigratedCorrespondencesHandler(repo.Object, dialog.Object, bg.Object, logger.Object);

        // Act
        await handler.ExecuteCleanupInBackground(10, CancellationToken.None);

        // Assert
        dialog.Verify(d => d.PatchCorrespondenceDialogToConfirmed(It.IsAny<Guid>()), Times.Never);
    }
}
