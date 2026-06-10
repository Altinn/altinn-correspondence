using Altinn.Correspondence.Application.CleanupMarkdownAndHTMLInSummary;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Hangfire;
using Microsoft.Extensions.Logging;
using Moq;
using Altinn.Correspondence.Common.Helpers;
using Xunit;
using Altinn.Correspondence.Tests.Factories;

namespace Altinn.Correspondence.Tests.TestingHandler;

public class CleanupMarkdownAndHTMLInSummaryHandlerTests
{
    [Fact]
    public async Task ExecuteCleanupInBackground_CleansMarkdownAndHtmlAndCountsAlreadyOk()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var c1 = new CorrespondenceEntityBuilder()
            .WithCreated(now.UtcDateTime.AddMinutes(-2))
            .WithMessageSummary("Some **markdown**")
            .WithExternalReference(ReferenceType.DialogportenDialogId, "d1")
            .Build();
        var c2 = new CorrespondenceEntityBuilder()
            .WithCreated(now.UtcDateTime.AddMinutes(-1))
            .WithMessageSummary("Already clean")
            .WithExternalReference(ReferenceType.DialogportenDialogId, "d2")
            .Build();

        var repo = new Mock<ICorrespondenceRepository>();
        repo.SetupSequence(r => r.GetCorrespondencesWindowAfter(
            It.IsAny<int>(),
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<Guid?>(),
            true,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CorrespondenceEntity> { c1, c2 })
            .ReturnsAsync(new List<CorrespondenceEntity>());
        repo.Setup(r => r.GetCorrespondencesByNoAltinn2IdAndExistingDialog(
                It.IsAny<List<Guid>>(),
                It.IsAny<ReferenceType>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<Guid> ids, ReferenceType _, CancellationToken __) =>
                new List<CorrespondenceEntity> { c1, c2 }.Where(x => ids.Contains(x.Id)).ToList());

        var dialog = new Mock<IDialogportenService>();
        var cleanedC1Summary = TextValidation.StripSummaryForHtmlAndMarkdown(c1.Content.MessageSummary);

        dialog.Setup(s => s.TryRemoveMarkdownAndHtmlFromSummary("d1", cleanedC1Summary, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        dialog.Setup(s => s.TryRemoveMarkdownAndHtmlFromSummary("d2", "Already clean", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var bg = new Mock<IBackgroundJobClient>();
        var logger = new Mock<ILogger<CleanupMarkdownAndHTMLInSummaryHandler>>();

        var handler = new CleanupMarkdownAndHTMLInSummaryHandler(repo.Object, dialog.Object, bg.Object, logger.Object);

        // Act
        await handler.ExecuteCleanupInBackground(100, CancellationToken.None);

        // Assert
        dialog.Verify(s => s.TryRemoveMarkdownAndHtmlFromSummary("d1", "Some markdown", It.IsAny<CancellationToken>()), Times.Once);
        dialog.Verify(s => s.TryRemoveMarkdownAndHtmlFromSummary("d2", "Already clean", It.IsAny<CancellationToken>()), Times.Never);
    }
}