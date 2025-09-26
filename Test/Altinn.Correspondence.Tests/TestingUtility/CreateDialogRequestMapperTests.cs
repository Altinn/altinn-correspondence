using Altinn.Correspondence.Integrations.Dialogporten.Mappers;
using Altinn.Correspondence.Tests.Factories;

namespace Altinn.Correspondence.Tests.TestingUtility;

public class CreateDialogRequestMapperTests
{
    [Fact]
    public void CreateCorrespondenceDialog_WithShortTitle_PreservesTitle()
    {
        // Arrange
        var correspondence = new CorrespondenceEntityBuilder()
            .WithMessageTitle("Short title")
            .Build();
        var baseUrl = "https://example.com";

        // Act
        var result = CreateDialogRequestMapper.CreateCorrespondenceDialog(correspondence, baseUrl);

        // Assert
        Assert.NotNull(result.Content);
        Assert.NotNull(result.Content.Title);
        Assert.Single(result.Content.Title.Value);
        Assert.Equal("Short title", result.Content.Title.Value[0].Value);
    }

    [Fact]
    public void CreateCorrespondenceDialog_WithTitleAt252Characters_PreservesTitle()
    {
        // Arrange
        var title252 = new string('A', 252); // Exactly 252 characters
        var correspondence = new CorrespondenceEntityBuilder()
            .WithMessageTitle(title252)
            .Build();
        var baseUrl = "https://example.com";

        // Act
        var result = CreateDialogRequestMapper.CreateCorrespondenceDialog(correspondence, baseUrl);

        // Assert
        Assert.NotNull(result.Content);
        Assert.NotNull(result.Content.Title);
        Assert.Single(result.Content.Title.Value);
        Assert.Equal(title252, result.Content.Title.Value[0].Value);
        Assert.Equal(252, result.Content.Title.Value[0].Value.Length);
    }

    [Fact]
    public void CreateCorrespondenceDialog_WithTitleAt255Characters_PreservesTitle()
    {
        // Arrange
        var title255 = new string('A', 255); // 255 characters - should be preserved as-is
        var correspondence = new CorrespondenceEntityBuilder()
            .WithMessageTitle(title255)
            .Build();
        var baseUrl = "https://example.com";

        // Act
        var result = CreateDialogRequestMapper.CreateCorrespondenceDialog(correspondence, baseUrl);

        // Assert
        Assert.NotNull(result.Content);
        Assert.NotNull(result.Content.Title);
        Assert.Single(result.Content.Title.Value);
        Assert.Equal(title255, result.Content.Title.Value[0].Value);
        Assert.Equal(255, result.Content.Title.Value[0].Value.Length);
    }

    [Fact]
    public void CreateCorrespondenceDialog_WithTitleAt256Characters_TruncatesTitle()
    {
        // Arrange
        var title256 = new string('A', 256); // 256 characters - should be truncated (first length to exceed 255)
        var correspondence = new CorrespondenceEntityBuilder()
            .WithMessageTitle(title256)
            .Build();
        var baseUrl = "https://example.com";

        // Act
        var result = CreateDialogRequestMapper.CreateCorrespondenceDialog(correspondence, baseUrl);

        // Assert
        Assert.NotNull(result.Content);
        Assert.NotNull(result.Content.Title);
        Assert.Single(result.Content.Title.Value);

        var resultTitle = result.Content.Title.Value[0].Value;
        Assert.Equal(255, resultTitle.Length); // 252 + 3 for "..."
        Assert.EndsWith("...", resultTitle);
        Assert.StartsWith(new string('A', 252), resultTitle);
    }

    [Fact]
    public void CreateCorrespondenceDialog_WithVeryLongTitle_TruncatesCorrectly()
    {
        // Arrange
        var veryLongTitle = new string('A', 500); // Very long title - should be truncated to exactly 255
        var correspondence = new CorrespondenceEntityBuilder()
            .WithMessageTitle(veryLongTitle)
            .Build();
        var baseUrl = "https://example.com";

        // Act
        var result = CreateDialogRequestMapper.CreateCorrespondenceDialog(correspondence, baseUrl);

        // Assert
        Assert.NotNull(result.Content);
        Assert.NotNull(result.Content.Title);
        Assert.Single(result.Content.Title.Value);

        var resultTitle = result.Content.Title.Value[0].Value;
        Assert.Equal(255, resultTitle.Length); // Should be exactly 255 (252 + "...")
        Assert.EndsWith("...", resultTitle);
        Assert.StartsWith(new string('A', 252), resultTitle);
    }

    [Fact]
    public void CreateCorrespondenceDialog_WithNullTitle_HandlesGracefully()
    {
        // Arrange
        var correspondence = new CorrespondenceEntityBuilder()
            .WithMessageTitle(null)
            .Build();
        var baseUrl = "https://example.com";

        // Act
        var result = CreateDialogRequestMapper.CreateCorrespondenceDialog(correspondence, baseUrl);

        // Assert
        Assert.NotNull(result.Content);
        Assert.NotNull(result.Content.Title);
        Assert.Single(result.Content.Title.Value);
        Assert.Equal("", result.Content.Title.Value[0].Value);
    }

    [Fact]
    public void CreateCorrespondenceDialog_WithEmptyTitle_HandlesGracefully()
    {
        // Arrange
        var correspondence = new CorrespondenceEntityBuilder()
            .WithMessageTitle("")
            .Build();
        var baseUrl = "https://example.com";

        // Act
        var result = CreateDialogRequestMapper.CreateCorrespondenceDialog(correspondence, baseUrl);

        // Assert
        Assert.NotNull(result.Content);
        Assert.NotNull(result.Content.Title);
        Assert.Single(result.Content.Title.Value);
        Assert.Equal("", result.Content.Title.Value[0].Value);
    }

    [Fact]
    public void CreateCorrespondenceDialog_WithHTMLInSummary_ReturnsPlainText()
    {
        // Arrange
        var htmlSummary = "<p>This is a <strong>test</strong> summary with <a href='#'>HTML</a> content.</p>";
        var htmlSummaryLength = htmlSummary.Length;
        var expectedPlainText = "This is a test summary with HTML content.";
        var correspondence = new CorrespondenceEntityBuilder()
            .WithMessageSummary(htmlSummary)
            .Build();
        var baseUrl = "https://example.com";

        // Act
        var result = CreateDialogRequestMapper.CreateCorrespondenceDialog(correspondence, baseUrl);
        var resultSummaryLength = result.Content?.Summary?.Value[0].Value.Length ?? 0;
        Assert.True(htmlSummaryLength > resultSummaryLength, $"Expected summary to be truncated. Original length: {htmlSummaryLength}, Result length: {resultSummaryLength}");
        // Assert
        Assert.NotNull(result.Content);
        Assert.NotNull(result.Content.Summary);
        Assert.Single(result.Content.Summary.Value);
        Assert.Equal(expectedPlainText, result.Content.Summary.Value[0].Value);
    }

    [Fact]
    public void CreateCorrespondenceDialog_WithMarkdownInSummary_ReturnsPlainText()
    {
        // Arrange
        var markdownSummary = "This is a **test** summary with [Markdown](https://example.com) content.";
        var markdownSummaryLength = markdownSummary.Length;
        var expectedPlainText = "This is a test summary with Markdown content.";
        var correspondence = new CorrespondenceEntityBuilder()
            .WithMessageSummary(markdownSummary)
            .Build();
        var baseUrl = "https://example.com";
        // Act
        var result = CreateDialogRequestMapper.CreateCorrespondenceDialog(correspondence, baseUrl);
        var resultSummaryLength = result.Content?.Summary?.Value[0].Value.Length ?? 0;
        Assert.True(markdownSummaryLength > resultSummaryLength, $"Expected summary to be truncated. Original length: {markdownSummaryLength}, Result length: {resultSummaryLength}");
        // Assert
        Assert.NotNull(result.Content);
        Assert.NotNull(result.Content.Summary);
        Assert.Single(result.Content.Summary.Value);
        Assert.Equal(expectedPlainText, result.Content.Summary.Value[0].Value);
    }
}