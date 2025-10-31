using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Integrations.Dialogporten.Mappers;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Core.Models.Entities;

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
        Assert.Equal("", result.Content.Title.Value[0].Value);  // The mapper will handle this gracefully, but DP will not accept empty titles, but this should not be possible to reach this point with null title
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
        Assert.Equal("", result.Content.Title.Value[0].Value); // The mapper will handle this gracefully, but DP will not accept empty titles, but this should not be possible to reach this point with empty title
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

    [Fact]
    public void CreateCorrespondenceDialog_WithFuturePublishTime_UpdatedAtShouldBeNow()
    {
        // Arrange
        DateTimeOffset currentUtcTime = DateTimeOffset.UtcNow;
        DateTimeOffset futureUtcTime = currentUtcTime.AddHours(1);

        var correspondence = new CorrespondenceEntityBuilder()
            .WithRequestedPublishTime(futureUtcTime)
            .WithStatus(CorrespondenceStatus.Published, futureUtcTime)
            .WithAltinn2CorrespondenceId(123)
            .Build();
        
        var baseUrl = "https://example.com";

        // Act
        var result = CreateDialogRequestMapper.CreateCorrespondenceDialog(correspondence, baseUrl, currentUtcNow: currentUtcTime);

        // Assert
        Assert.NotNull(result.UpdatedAt);
        Assert.Equal(currentUtcTime, result.UpdatedAt.Value);
    }

    [Fact]
    public void CreateCorrespondenceDialog_WithOldPublished_UpdatedAtShouldThen()
    {
        // Arrange
        DateTimeOffset currentUtcTime = DateTimeOffset.UtcNow;
        DateTimeOffset originalPublishDate = currentUtcTime.AddHours(-1);

        var correspondence = new CorrespondenceEntityBuilder()
            .WithRequestedPublishTime(originalPublishDate)
            .WithStatus(CorrespondenceStatus.Published, originalPublishDate)
            .WithAltinn2CorrespondenceId(123)
            .Build();

        var baseUrl = "https://example.com";

        // Act
        var result = CreateDialogRequestMapper.CreateCorrespondenceDialog(correspondence, baseUrl, currentUtcNow: currentUtcTime);

        // Assert        
        Assert.NotNull(result.UpdatedAt);
        Assert.Equal(originalPublishDate, result.UpdatedAt.Value);
    }

    [Fact]
    public void CreateCorrespondenceDialog_WithEmailNotifications_ShouldCreateCorrectActivities()
    {
        // Arrange
        var correspondence = new CorrespondenceEntityBuilder()
            .WithMessageTitle("Test Message")
            .WithExternalReference(ReferenceType.DialogportenDialogId, "dialog-id")
            .Build();

        var initialNotification = new CorrespondenceNotificationEntity
        {
            Id = Guid.NewGuid(),
            CorrespondenceId = correspondence.Id,
            NotificationChannel = NotificationChannel.Email,
            NotificationAddress = "test@example.com",
            IsReminder = false, // Initial notification
            NotificationSent = DateTimeOffset.UtcNow.AddMinutes(-10),
            Altinn2NotificationId = 12345,
            NotificationTemplate = NotificationTemplate.GenericAltinnMessage,
            Created = DateTimeOffset.UtcNow
        };

        var reminderNotification = new CorrespondenceNotificationEntity
        {
            Id = Guid.NewGuid(),
            CorrespondenceId = correspondence.Id,
            NotificationChannel = NotificationChannel.Email,
            NotificationAddress = "test@example.com",
            IsReminder = true, // Reminder notification
            NotificationSent = DateTimeOffset.UtcNow.AddMinutes(-5),
            Altinn2NotificationId = 12346,
            NotificationTemplate = NotificationTemplate.GenericAltinnMessage,
            Created = DateTimeOffset.UtcNow
        };

        correspondence.Notifications = new List<CorrespondenceNotificationEntity> { initialNotification, reminderNotification };

        var baseUrl = "https://example.com";

        // Act
        var result = CreateDialogRequestMapper.CreateCorrespondenceDialog(correspondence, baseUrl, includeActivities: true);

        // Assert
        Assert.NotNull(result.Activities);
        var notificationActivities = result.Activities.Where(a => a.Type == "Information").ToList();
        Assert.Equal(2, notificationActivities.Count);

        // Find initial notification activity
        var initialActivity = notificationActivities.FirstOrDefault(a => 
            a.Description?.Any(d => d.Value.Contains("Varsel om mottatt melding")) == true);
        Assert.NotNull(initialActivity);

        // Verify initial notification text in Norwegian and English
        var initialNbDescription = initialActivity.Description.FirstOrDefault(d => d.LanguageCode == "nb");
        Assert.NotNull(initialNbDescription);
        Assert.Equal("Varsel om mottatt melding sendt til test@example.com på Email.", initialNbDescription.Value);

        var initialEnDescription = initialActivity.Description.FirstOrDefault(d => d.LanguageCode == "en");
        Assert.NotNull(initialEnDescription);
        Assert.Equal("Notification about received message sent to test@example.com on Email.", initialEnDescription.Value);

        // Find reminder notification activity
        var reminderActivity = notificationActivities.FirstOrDefault(a => 
            a.Description?.Any(d => d.Value.Contains("Revarsel om mottatt melding")) == true);
        Assert.NotNull(reminderActivity);

        // Verify reminder notification text in Norwegian and English
        var reminderNbDescription = reminderActivity.Description.FirstOrDefault(d => d.LanguageCode == "nb");
        Assert.NotNull(reminderNbDescription);
        Assert.Equal("Revarsel om mottatt melding sendt til test@example.com på Email.", reminderNbDescription.Value);

        var reminderEnDescription = reminderActivity.Description.FirstOrDefault(d => d.LanguageCode == "en");
        Assert.NotNull(reminderEnDescription);
        Assert.Equal("Reminder notification about received message sent to test@example.com on Email.", reminderEnDescription.Value);
    }

    [Fact]
    public void CreateCorrespondenceDialog_WithSmsNotifications_ShouldCreateCorrectActivities()
    {
        // Arrange
        var correspondence = new CorrespondenceEntityBuilder()
            .WithMessageTitle("Test Message")
            .WithExternalReference(ReferenceType.DialogportenDialogId, "dialog-id")
            .Build();

        var initialNotification = new CorrespondenceNotificationEntity
        {
            Id = Guid.NewGuid(),
            CorrespondenceId = correspondence.Id,
            NotificationChannel = NotificationChannel.Sms,
            NotificationAddress = "+4712345678",
            IsReminder = false, // Initial notification
            NotificationSent = DateTimeOffset.UtcNow.AddMinutes(-10),
            Altinn2NotificationId = 12345,
            NotificationTemplate = NotificationTemplate.GenericAltinnMessage,
            Created = DateTimeOffset.UtcNow
        };

        var reminderNotification = new CorrespondenceNotificationEntity
        {
            Id = Guid.NewGuid(),
            CorrespondenceId = correspondence.Id,
            NotificationChannel = NotificationChannel.Sms,
            NotificationAddress = "+4712345678",
            IsReminder = true, // Reminder notification
            NotificationSent = DateTimeOffset.UtcNow.AddMinutes(-5),
            Altinn2NotificationId = 12346,
            NotificationTemplate = NotificationTemplate.GenericAltinnMessage,
            Created = DateTimeOffset.UtcNow
        };

        correspondence.Notifications = new List<CorrespondenceNotificationEntity> { initialNotification, reminderNotification };

        var baseUrl = "https://example.com";

        // Act
        var result = CreateDialogRequestMapper.CreateCorrespondenceDialog(correspondence, baseUrl, includeActivities: true);

        // Assert
        Assert.NotNull(result.Activities);
        var notificationActivities = result.Activities.Where(a => a.Type == "Information").ToList();
        Assert.Equal(2, notificationActivities.Count);

        // Find initial notification activity
        var initialActivity = notificationActivities.FirstOrDefault(a => 
            a.Description?.Any(d => d.Value.Contains("Varsel om mottatt melding")) == true);
        Assert.NotNull(initialActivity);

        // Verify initial SMS notification text in Norwegian and English
        var initialNbDescription = initialActivity.Description.FirstOrDefault(d => d.LanguageCode == "nb");
        Assert.NotNull(initialNbDescription);
        Assert.Equal("Varsel om mottatt melding sendt til +4712345678 på SMS.", initialNbDescription.Value);

        var initialEnDescription = initialActivity.Description.FirstOrDefault(d => d.LanguageCode == "en");
        Assert.NotNull(initialEnDescription);
        Assert.Equal("Notification about received message sent to +4712345678 on SMS.", initialEnDescription.Value);

        // Find reminder notification activity
        var reminderActivity = notificationActivities.FirstOrDefault(a => 
            a.Description?.Any(d => d.Value.Contains("Revarsel om mottatt melding")) == true);
        Assert.NotNull(reminderActivity);

        // Verify reminder SMS notification text in Norwegian and English
        var reminderNbDescription = reminderActivity.Description.FirstOrDefault(d => d.LanguageCode == "nb");
        Assert.NotNull(reminderNbDescription);
        Assert.Equal("Revarsel om mottatt melding sendt til +4712345678 på SMS.", reminderNbDescription.Value);

        var reminderEnDescription = reminderActivity.Description.FirstOrDefault(d => d.LanguageCode == "en");
        Assert.NotNull(reminderEnDescription);
        Assert.Equal("Reminder notification about received message sent to +4712345678 on SMS.", reminderEnDescription.Value);
    }

}