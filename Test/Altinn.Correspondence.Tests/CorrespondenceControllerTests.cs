using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.CancelNotification;
using Altinn.Correspondence.Application.Configuration;
using Altinn.Correspondence.Application.GetCorrespondences;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Helpers;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Slack.Webhooks;
using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Altinn.Correspondence.Tests;

public class CorrespondenceControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _senderClient;
    private readonly HttpClient _recipientClient;
    private readonly JsonSerializerOptions _responseSerializerOptions;

    public CorrespondenceControllerTests(CustomWebApplicationFactory factory)
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

    [Fact]
    public async Task InitializeCorrespondence()
    {
        // Arrange
        var correspondence = new CorrespondenceBuilder().CreateCorrespondence().Build();

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);

        // Assert
        Assert.Equal(HttpStatusCode.OK, initializeCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task InitializeCorrespondence_WithCorrectLanguageCode_ReturnsOK()
    {
        // Arrange
        var payload1 = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithLanguageCode("nN")
            .Build();
        var payload2 = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithLanguageCode("EN")
            .Build();

        // Act
        var response1 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload1);
        var response2 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload2);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
    }

    [Fact]
    public async Task InitializeCorrespondence_WithInvalidLanguageCode_ReturnsBadRequest()
    {
        // Arrange
        var payload1 = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithLanguageCode("nu")
            .Build();
        var payload2 = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithLanguageCode(null)
            .Build();
        var payload3 = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithLanguageCode("")
            .Build();

        // Act
        var response1 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload1);
        var response2 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload2);
        var response3 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload3);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response1.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, response2.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, response3.StatusCode);
    }

    [Fact]
    public async Task InitializeCorrespondence_WithExistingAttachmentsPublished_ReturnsOK()
    {
        // Arrange
        var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithExistingAttachments([attachmentId])
            .Build();

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

        // Assert
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task InitializeCorrespondenceMultiple_WithExistingAttachmentsPublished_ReturnsOK()
    {
        var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithExistingAttachments([attachmentId])
            .WithRecipients(["0192:986252932", "0198:991234649"])
            .Build();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task InitializeCorrespondence_WithInvalidExistingAttachments_ReturnsBadRequest()
    {
        // Arrange
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithExistingAttachments([Guid.NewGuid()])
            .Build();

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task InitializeCorrespondence_WithExistingAttachmentsNotPublished_ReturnsBadRequest()
    {
        // Arrange
        var attachmentId = await AttachmentHelper.GetInitializedAttachment(_senderClient, _responseSerializerOptions);
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithExistingAttachments([attachmentId])
            .Build();

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
    }
    
    [Fact]
    public async Task InitializeCorrespondence_WithoutContent_ReturnsBadRequest()
    {
        // Arrange
        var correspondence = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithCorrespondenceContent(null)
            .Build();

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
    }
    
    [Fact]
    public async Task InitializeCorrespondence_WithEmptyMessageFields_ReturnsBadRequest()
    {
        // Arrange
        var payload1 = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithMessageTitle("")
            .Build();

        var payload2 = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithMessageSummary("")
            .Build();
        
        var payload3 = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithMessageBody("")
            .Build();

        // Act
        var response1 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload1);
        var response2 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload2);
        var response3 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload3);


        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response1.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, response2.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, response3.StatusCode);
    }

    [Fact]
    public async Task InitializeCorrespondence_With_HTML_Or_Markdown_In_Title_fails()
    {
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithMessageTitle("<h1>test</h1>")
            .Build();

        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);

        payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithMessageTitle("# test")
            .Build();
        initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task InitializeCorrespondence_With_HTML_In_Summary_Or_Body_fails()
    {
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithMessageSummary("<h1>test</h1>")
            .Build();

        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);

        payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithMessageBody("<h1>test</h1>")
            .Build();

        initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task InitializeCorrespondence_No_Message_Body_fails()
    {
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithMessageBody(null)
            .Build();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task InitializeCorrespondence_With_Different_Markdown_In_Body()
    {
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithMessageBody(File.ReadAllText("Data/Markdown.text"))
            .Build();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task InitializeCorrespondence_Recipient_Can_Handle_Org_And_Ssn()
    {
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithRecipients(["1234:123456789", "12345678901"])
            .Build();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task InitializeCorrespondence_With_Invalid_Sender_Returns_BadRequest()
    {
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithSender("invalid-sender")
            .Build();

        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task InitializeCorrespondence_With_Invalid_Recipient_Returns_BadRequest()
    {
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithRecipients(["invalid-recipient"])
            .Build();

        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);

        payload.Recipients = ["123456789"];
        initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);

        payload.Recipients = ["1234567812390123"];
        initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task InitializeCorrespondence_As_Recipient_Is_Forbidden()
    {
        // Arrange
        var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();

        // Act
        var initializeCorrespondenceResponse = await _recipientClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, initializeCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task InitializeCorrespondence_DueDate_PriorToday_Returns_BadRequest()
    {
        // Arrange
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithDueDateTime(DateTimeOffset.UtcNow.AddDays(-7))
            .Build();

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task InitializeCorrespondence_DueDate_PriorRequestedPublishTime_Returns_BadRequest()
    {
        // Arrange
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithDueDateTime(DateTimeOffset.UtcNow.AddDays(7))
            .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(14))
            .Build();

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task InitializeCorrespondence_AllowSystemDeleteAfter_PriorToday_Returns_BadRequest()
    {
        // Arrange
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithAllowSystemDeleteAfter(DateTimeOffset.UtcNow.AddDays(-7))
            .Build();

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task InitializeCorrespondence_AllowSystemDeleteAfter_PriorRequestedPublishTime_Returns_BadRequest()
    {
        // Arrange
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithAllowSystemDeleteAfter(DateTimeOffset.UtcNow.AddDays(7))
            .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(14))
            .WithDueDateTime(DateTimeOffset.UtcNow.AddDays(21)) // ensure DueDate is after RequestedPublishTime
            .Build();

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task InitializeCorrespondence_AllowSystemDeleteAfter_PriorDueDate_Returns_BadRequest()
    {
        // Arrange
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithAllowSystemDeleteAfter(DateTimeOffset.UtcNow.AddDays(14))
            .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(7))
            .WithDueDateTime(DateTimeOffset.UtcNow.AddDays(21))
            .Build();

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task InitializeCorrespondence_WithConfirmationNeeded_Without_DueDate_Returns_BadRequest()
    {
        // Arrange
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithConfirmationNeeded(true)
            .Build();

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task InitializeCorrespondence_With_RequestedPublishTime_Null()
    {
    // Arrange
        var correspondence = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithRequestedPublishTime(null)
            .Build();

        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);

        // Assert
        Assert.Equal(HttpStatusCode.OK, initializeCorrespondenceResponse.StatusCode);
    }
    
    [Fact]
    public async Task UploadCorrespondence_Gives_Ok()
    {
        // Arrange
        using var stream = File.OpenRead("./Data/Markdown.text");
        var file = new FormFile(stream, 0, stream.Length, null, Path.GetFileName(stream.Name));
        var attachmentData = AttachmentHelper.GetAttachmentMetaData(file.FileName);
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithAttachments([attachmentData])
            .Build();
        var formData = CorrespondenceToFormData(payload.Correspondence);
        formData.Add(new StringContent("0192:986252932"), "recipients[0]");
        using var fileStream = file.OpenReadStream();
        formData.Add(new StreamContent(fileStream), "attachments", file.FileName);

        // Act
        var uploadCorrespondenceResponse = await _senderClient.PostAsync("correspondence/api/v1/correspondence/upload", formData);
        // Assert
        Assert.True(uploadCorrespondenceResponse.IsSuccessStatusCode, await uploadCorrespondenceResponse.Content.ReadAsStringAsync());

        // Arrange
        var response = await uploadCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        var attachmentId = response?.AttachmentIds.FirstOrDefault();
        var attachmentOverview = await _senderClient.GetFromJsonAsync<AttachmentOverviewExt>($"correspondence/api/v1/attachment/{attachmentId}", _responseSerializerOptions);
        var newAttachmentData = AttachmentHelper.GetAttachmentMetaData("Logical file name", attachmentOverview);
        var payload2 = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithAttachments([attachmentData, newAttachmentData])
            .Build();
        formData = CorrespondenceToFormData(payload2.Correspondence);
        formData.Add(new StreamContent(fileStream), "attachments", file.FileName);

        // Act
        var uploadCorrespondenceResponse2 = await _senderClient.PostAsync("correspondence/api/v1/correspondence/upload", formData);
        // Assert
        Assert.True(uploadCorrespondenceResponse.IsSuccessStatusCode, await uploadCorrespondenceResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UploadCorrespondenceWithoutAttachments_Gives_Ok()
    {
        // Arrange
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .Build();
        var formData = CorrespondenceToFormData(payload.Correspondence);
        formData.Add(new StringContent("0192:986252932"), "recipients[0]");

        // Act
        var uploadCorrespondenceResponse = await _senderClient.PostAsync("correspondence/api/v1/correspondence/upload", formData);

        // Assert
        Assert.True(uploadCorrespondenceResponse.IsSuccessStatusCode, await uploadCorrespondenceResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UploadCorrespondence_WithoutAttachments_ReturnsUnsupportedMediaType()
    {
        // Arrange
        var correspondence = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .Build();

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence/upload", correspondence);

        // Assert
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, initializeCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task UploadCorrespondence_With_Multiple_Files()
    {
        using var stream = System.IO.File.OpenRead("./Data/Markdown.text");
        var file = new FormFile(stream, 0, stream.Length, null, Path.GetFileName(stream.Name));
        using var fileStream = file.OpenReadStream();
        using var stream2 = System.IO.File.OpenRead("./Data/test.txt");
        var file2 = new FormFile(stream2, 0, stream2.Length, null, Path.GetFileName(stream2.Name));
        using var fileStream2 = file2.OpenReadStream();

        var attachmentMetaData = AttachmentHelper.GetAttachmentMetaData(file.FileName);
        var attachmentMetaData2 = AttachmentHelper.GetAttachmentMetaData(file2.FileName);
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithRecipients(["0192:986252932"])
            .WithAttachments([attachmentMetaData, attachmentMetaData2])
            .Build();
        var formData = CorrespondenceToFormData(payload.Correspondence);
        formData.Add(new StringContent("0192:986252932"), "recipients[0]");
        formData.Add(new StreamContent(fileStream), "attachments", file.FileName);
        formData.Add(new StreamContent(fileStream2), "attachments", file2.FileName);

        var uploadCorrespondenceResponse = await _senderClient.PostAsync("correspondence/api/v1/correspondence/upload", formData);
        Assert.Equal(HttpStatusCode.OK, uploadCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task UploadCorrespondence_No_Files_Gives_Bad_request()
    {
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithAttachments([])
            .Build();
        var formData = CorrespondenceToFormData(payload.Correspondence);
        var uploadCorrespondenceResponse = await _senderClient.PostAsync("correspondence/api/v1/correspondence/upload", formData);
        Assert.Equal(HttpStatusCode.BadRequest, uploadCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task UploadCorrespondences_With_Multiple_Files()
    {
        using var stream = File.OpenRead("./Data/Markdown.text");
        var file = new FormFile(stream, 0, stream.Length, null, Path.GetFileName(stream.Name));
        using var fileStream = file.OpenReadStream();
        var attachmentMetaData = AttachmentHelper.GetAttachmentMetaData(file.FileName);

        using var stream2 = File.OpenRead("./Data/test.txt");
        var file2 = new FormFile(stream2, 0, stream2.Length, null, Path.GetFileName(stream2.Name));
        using var fileStream2 = file2.OpenReadStream();
        var attachmentMetaData2 = AttachmentHelper.GetAttachmentMetaData(file2.FileName);

        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithRecipients(["0192:986252932"])
            .WithAttachments([attachmentMetaData, attachmentMetaData2])
            .Build();

        var formData = CorrespondenceToFormData(payload.Correspondence);
        formData.Add(new StreamContent(fileStream), "attachments", file.FileName);
        formData.Add(new StreamContent(fileStream2), "attachments", file2.FileName);
        formData.Add(new StringContent("0192:986252932"), "recipients[0]");
        formData.Add(new StringContent("0198:991234649"), "recipients[1]");

        var uploadCorrespondenceResponse = await _senderClient.PostAsync("correspondence/api/v1/correspondence/upload", formData);
        Assert.True(uploadCorrespondenceResponse.IsSuccessStatusCode, await uploadCorrespondenceResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetCorrespondences()
    {
        var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());

        var correspondenceList = await _senderClient.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={1}&offset={0}&limit={10}&status={0}&role={"recipientandsender"}");
        Assert.True(correspondenceList?.Pagination.TotalItems > 0);
    }

    [Fact]
    public async Task GetCorrespondences_WithInvalidRole_ReturnsBadRequest()
    {
        var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());

        var responseWithout = await _senderClient.GetAsync($"correspondence/api/v1/correspondence?resourceId={1}&offset={0}&limit={10}&status={0}");
        Assert.Equal(HttpStatusCode.BadRequest, responseWithout.StatusCode);

        var responseWithInvalid = await _senderClient.GetAsync($"correspondence/api/v1/correspondence?resourceId={1}&offset={0}&limit={10}&status={0}&role={"invalid"}");
        Assert.Equal(HttpStatusCode.BadRequest, responseWithInvalid.StatusCode);
    }

    [Fact]
    public async Task GetCorrespondencesOnlyFromSearchedResourceId()
    {
        var resourceA = Guid.NewGuid().ToString();
        var resourceB = Guid.NewGuid().ToString();
        var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);

        var payloadResourceA = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithExistingAttachments([attachmentId])
            .WithResourceId(resourceA)
            .Build();
        var payloadResourceB = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithExistingAttachments([attachmentId])
            .WithResourceId(resourceB)
            .Build();

        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payloadResourceA);
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());

        var initializeCorrespondenceResponse2 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payloadResourceB);
        Assert.True(initializeCorrespondenceResponse2.IsSuccessStatusCode, await initializeCorrespondenceResponse2.Content.ReadAsStringAsync());

        int status = (int)CorrespondenceStatusExt.Published;
        var correspondenceList = await _senderClient.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={resourceA}&offset={0}&limit={10}&status={status}&role={"recipientandsender"}");
        Assert.Equal(correspondenceList?.Pagination.TotalItems, payloadResourceA.Recipients.Count);
    }

    [Fact]
    public async Task GetCorrespondences_When_IsSender_Or_IsRecipient_Specified_ReturnsSpecifiedCorrespondences()
    {
        // Arrange
        var resource = Guid.NewGuid().ToString();
        var recipientId = "0192:000000000";
        var senderId = "0192:111111111";
        var externalId = "0192:222222222";

        // Create correspondence as Sender with recipientId amongst recipients
        var senderPayload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithResourceId(resource)
            .WithSender(senderId)
            .WithRecipients([recipientId, "0192:123456789", "0192:321654987"])
            .Build();
        var senderClient = _factory.CreateClientWithAddedClaims(
            ("consumer", $"{{\"authority\":\"iso6523-actorid-upis\",\"ID\":\"{senderId}\"}}"),
            ("scope", AuthorizationConstants.SenderScope)
        );
        var initResponse = await senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", senderPayload);
        Assert.True(initResponse.IsSuccessStatusCode, await initResponse.Content.ReadAsStringAsync());

        // Create some correspondences with External sender with senderId and recipientId amongst recipients
        var externalPayload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithResourceId(resource)
            .WithSender(externalId)
            .WithRecipients([senderId, recipientId, "0192:864231509"])
            .Build();
        var externalClient = _factory.CreateClientWithAddedClaims(
            ("consumer", $"{{\"authority\":\"iso6523-actorid-upis\",\"ID\":\"{externalId}\"}}"),
            ("scope", AuthorizationConstants.SenderScope)
        );
        var externalInitResponse = await externalClient.PostAsJsonAsync("correspondence/api/v1/correspondence", externalPayload);
        Assert.True(externalInitResponse.IsSuccessStatusCode, await externalInitResponse.Content.ReadAsStringAsync());

        // Create recipient client to retrieve correspondences with correct ID
        var recipientIdClient = _factory.CreateClientWithAddedClaims(
            ("consumer", $"{{\"authority\":\"iso6523-actorid-upis\",\"ID\":\"{recipientId}\"}}"),
            ("scope", AuthorizationConstants.RecipientScope)
        );

        // Act
        var correspondencesSender = await senderClient.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={resource}&offset=0&limit=10&role={"sender"}");
        var correspondencesRecipient = await recipientIdClient.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={resource}&offset=0&limit=10&role={"recipient"}");
        var correspondencesSenderAndRecipient = await senderClient.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={resource}&offset=0&limit=10&role={"recipientandsender"}");

        // Assert
        var expectedSender = senderPayload.Recipients.Count; // sender only sees the ones they sent
        Assert.Equal(expectedSender, correspondencesSender?.Pagination.TotalItems);
        var expectedRecipient = senderPayload.Recipients.Where(r => r == recipientId).Count() + externalPayload.Recipients.Where(r => r == recipientId).Count(); // recipient sees the ones from the initial sender and external sender
        Assert.Equal(expectedRecipient, correspondencesRecipient?.Pagination.TotalItems);
        var expectedSenderAndRecipient = expectedSender + externalPayload.Recipients.Where(r => r == senderId).Count(); // sender sees the ones they sent and the ones where they were the recipient from external
        Assert.Equal(expectedSenderAndRecipient, correspondencesSenderAndRecipient?.Pagination.TotalItems);
    }

    [Fact]
    public async Task GetCorrespondences_WithStatusSpecified_ShowsSpecifiedCorrespondences()
    {
        // Arrange
        var resourceId = Guid.NewGuid().ToString();
        var initializedCorrespondence = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithResourceId(resourceId)
            .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(1))
            .Build();
        var a = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", initializedCorrespondence);
        var correspondence = await a.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        var getCorrespondenceOverviewResponse = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.Correspondences.FirstOrDefault().CorrespondenceId}");
        var b = await getCorrespondenceOverviewResponse.Content.ReadFromJsonAsync<CorrespondenceOverviewExt>(_responseSerializerOptions);

        var publishedCorrespondences = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithResourceId(resourceId)
            .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(-1))
            .Build();
        await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", publishedCorrespondences);

        // Act
        var responseWithInitialized = await _senderClient.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={resourceId}&offset=0&limit=10&status={1}&role={"sender"}");
        var responseWithPublished = await _senderClient.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={resourceId}&offset=0&limit=10&status={2}&role={"sender"}");

        // Assert
        var expectedInitialized = initializedCorrespondence.Recipients.Count;
        Assert.Equal(expectedInitialized, responseWithInitialized?.Pagination.TotalItems);
        var expectedPublished = publishedCorrespondences.Recipients.Count;
        Assert.Equal(expectedPublished, responseWithPublished?.Pagination.TotalItems);
    }

    [Fact]
    public async Task GetCorrespondences_WithoutStatusSpecified_AsReceiver_ReturnsAllExceptBlacklisted()
    {
        // Arrange
        var resource = Guid.NewGuid().ToString();
        var recipientId = "0192:000000000";
        var recipientClient = _factory.CreateClientWithAddedClaims(
            ("consumer", $"{{\"authority\":\"iso6523-actorid-upis\",\"ID\":\"{recipientId}\"}}"),
            ("scope", AuthorizationConstants.RecipientScope)
        );

        var payloadPublished = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithResourceId(resource)
            .WithRecipients([recipientId])
            .Build(); // One published

        var payloadReadyForPublish = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithResourceId(resource)
            .WithRecipients([recipientId])
            .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(1))
            .Build(); // One ready for publish

        // Act
        var responsePublished = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payloadPublished);
        Assert.True(responsePublished.IsSuccessStatusCode, await responsePublished.Content.ReadAsStringAsync());
        var responseReadyForPublish = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payloadReadyForPublish);
        Assert.True(responseReadyForPublish.IsSuccessStatusCode, await responseReadyForPublish.Content.ReadAsStringAsync());
        var correspondenceList = await recipientClient.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={resource}&offset=0&limit=10&role={"recipient"}");

        // Assert
        var expected = payloadPublished.Recipients.Where(r => r == recipientId).Count(); // Receiver only sees the one that is published
        Assert.Equal(expected, correspondenceList?.Pagination.TotalItems);
    }

    [Fact]
    public async Task GetCorrespondences_WithoutStatusSpecified_AsSender_ReturnsAllExceptBlacklisted()
    {
        // Arrange
        var resource = Guid.NewGuid().ToString();
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithResourceId(resource)
            .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(1))
            .Build(); // One ReadyForPublish

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());
        var correspondencesBeforeDeletion = await _senderClient.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={resource}&offset=0&limit=10&role={"sender"}");
        var response = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);

        await _senderClient.DeleteAsync($"correspondence/api/v1/correspondence/{response.Correspondences.FirstOrDefault().CorrespondenceId}/purge");
        var correspondencesAfterDeletion = await _senderClient.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={resource}&offset=0&limit=10&role={"sender"}");

        // Assert
        var expectedBeforeDeletion = payload.Recipients.Count;
        Assert.Equal(correspondencesBeforeDeletion?.Pagination.TotalItems, expectedBeforeDeletion);
        var expectedAfterDeletion = payload.Recipients.Count - 1; // One was deleted
        Assert.Equal(expectedAfterDeletion, correspondencesAfterDeletion?.Pagination.TotalItems);
    }

    [Fact]
    public async Task GetCorrespondences_WithStatusSpecified_ButStatusIsBlackListed_DoesNotReturnCorrespondence()
    {
        // Arrange
        var resource = Guid.NewGuid().ToString();
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithResourceId(resource)
            .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(1))
            .Build(); // One ReadyForPublish

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());
        var correspondencesRecipient = await _recipientClient.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={resource}&offset=0&limit=10&status={1}&role={"recipient"}");
        var correspondencesSender = await _senderClient.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={resource}&offset=0&limit=10&status={1}&role={"sender"}");

        // Assert
        var expectedRecipient = 0; // recipient does not see ReadyForPublish
        var expectedSender = payload.Recipients.Count;
        Assert.Equal(expectedRecipient, correspondencesRecipient?.Pagination.TotalItems);
        Assert.Equal(expectedSender, correspondencesSender?.Pagination.TotalItems);
    }

    [Fact]
    public async Task GetCorrespondenceOverview()
    {
        // Arrange
        var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);

        // Act
        var getCorrespondenceOverviewResponse = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.Correspondences.FirstOrDefault().CorrespondenceId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, getCorrespondenceOverviewResponse.StatusCode);
    }

    [Fact]
    public async Task GetCorrespondenceOverview_WhenNotSenderOrRecipient_Returns404()
    {
        // Arrange
        var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
        var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        var invalidSenderClient = _factory.CreateClientWithAddedClaims(("consumer", "{\"authority\":\"iso6523-actorid-upis\",\"ID\":\"0192:123456789\"}"), ("scope", AuthorizationConstants.SenderScope));
        var invalidRecipientClient = _factory.CreateClientWithAddedClaims(("consumer", "{\"authority\":\"iso6523-actorid-upis\",\"ID\":\"0192:123456789\"}"), ("scope", AuthorizationConstants.RecipientScope));

        // Act
        var invalidRecipientResponse = await invalidRecipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.Correspondences.FirstOrDefault().CorrespondenceId}");
        var invalidSenderResponse = await invalidSenderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.Correspondences.FirstOrDefault().CorrespondenceId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, invalidRecipientResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, invalidSenderResponse.StatusCode);
    }

    [Fact]
    public async Task GetCorrespondenceOverview_AsReceiver_WhenNotPublishedReturns404()
    {
        // Arrange
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(1))
            .Build();

        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);

        // Act
        var getCorrespondenceOverviewResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.Correspondences.FirstOrDefault().CorrespondenceId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, getCorrespondenceOverviewResponse.StatusCode);
    }

    [Fact]
    public async Task GetCorrespondenceOverview_AsReceiver_AddsFetchedStatusToHistory()
    {
        // Arrange
        var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);

        // Act
        var getCorrespondenceOverviewResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.Correspondences.FirstOrDefault().CorrespondenceId}");
        Assert.True(getCorrespondenceOverviewResponse.IsSuccessStatusCode, await getCorrespondenceOverviewResponse.Content.ReadAsStringAsync());

        // Assert
        var response = await getCorrespondenceOverviewResponse.Content.ReadFromJsonAsync<CorrespondenceOverviewExt>(_responseSerializerOptions);
        Assert.Equal(CorrespondenceStatusExt.Published, response.Status); // Status is not changed to fetched
        var actual = await (await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.Correspondences.FirstOrDefault().CorrespondenceId}/details")).Content.ReadFromJsonAsync<CorrespondenceDetailsExt>(_responseSerializerOptions);

        var expectedFetchedStatuses = 1;
        Assert.Equal(actual.StatusHistory.Where(s => s.Status == CorrespondenceStatusExt.Fetched).Count(), expectedFetchedStatuses);
        Assert.Contains(actual.StatusHistory, item => item.Status == CorrespondenceStatusExt.Published);
    }

    [Fact]
    public async Task GetCorrespondenceOverview_AsSender_KeepsStatusAsPublished()
    {
        // Arrange
        var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);

        // Act
        var getCorrespondenceOverviewResponse = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.Correspondences.FirstOrDefault().CorrespondenceId}");
        Assert.True(getCorrespondenceOverviewResponse.IsSuccessStatusCode, await getCorrespondenceOverviewResponse.Content.ReadAsStringAsync());

        // Assert
        var response = await getCorrespondenceOverviewResponse.Content.ReadFromJsonAsync<CorrespondenceOverviewExt>(_responseSerializerOptions);
        var getCorrespondenceDetailsResponse = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.Correspondences.FirstOrDefault().CorrespondenceId}/details");
        var detailsResponse = await getCorrespondenceDetailsResponse.Content.ReadFromJsonAsync<CorrespondenceDetailsExt>(_responseSerializerOptions);
        Assert.DoesNotContain(detailsResponse.StatusHistory, item => item.Status == CorrespondenceStatusExt.Fetched); // Fetched is not added to the list
        Assert.Equal(CorrespondenceStatusExt.Published, response.Status);
    }

    [Fact]
    public async Task GetCorrespondenceDetails()
    {
        // Arrange
        var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
        var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);

        // Act
        var getCorrespondenceDetailsResponse = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.Correspondences.FirstOrDefault().CorrespondenceId}/details");

        // Assert
        Assert.Equal(HttpStatusCode.OK, getCorrespondenceDetailsResponse.StatusCode);
    }

    [Fact]
    public async Task GetCorrespondenceDetails_WhenNotSenderOrRecipient_Returns404()
    {
        // Arrange
        var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
        var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        var invalidSenderClient = _factory.CreateClientWithAddedClaims(("consumer", "{\"authority\":\"iso6523-actorid-upis\",\"ID\":\"0192:123456789\"}"), ("scope", AuthorizationConstants.SenderScope));
        var invalidRecipientClient = _factory.CreateClientWithAddedClaims(("consumer", "{\"authority\":\"iso6523-actorid-upis\",\"ID\":\"0192:123456789\"}"), ("scope", AuthorizationConstants.RecipientScope));

        // Act
        var invalidRecipientResponse = await invalidRecipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.Correspondences.FirstOrDefault().CorrespondenceId}/details");
        var invalidSenderResponse = await invalidSenderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.Correspondences.FirstOrDefault().CorrespondenceId}/details");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, invalidRecipientResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, invalidSenderResponse.StatusCode);
    }

    [Fact]
    public async Task GetCorrespondenceDetails_AsReceiver_WhenNotPublishedReturns404()
    {
        // Arrange
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(1))
            .Build();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);

        // Act
        var getCorrespondenceDetailsResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.Correspondences.FirstOrDefault().CorrespondenceId}/details");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, getCorrespondenceDetailsResponse.StatusCode);
    }

    [Fact]
    public async Task GetCorrespondenceDetails_AsReceiver_AddsFetchedStatusToHistory()
    {
        // Arrange
        var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
        var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);

        // Act
        var getCorrespondenceDetailsResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.Correspondences.FirstOrDefault().CorrespondenceId}/details");
        Assert.True(getCorrespondenceDetailsResponse.IsSuccessStatusCode, await getCorrespondenceDetailsResponse.Content.ReadAsStringAsync());

        // Assert
        var response = await getCorrespondenceDetailsResponse.Content.ReadFromJsonAsync<CorrespondenceDetailsExt>(_responseSerializerOptions);
        var expectedFetchedStatuses = 1;
        Assert.Equal(response.StatusHistory.Where(s => s.Status == CorrespondenceStatusExt.Fetched).Count(), expectedFetchedStatuses);
        Assert.Contains(response.StatusHistory, item => item.Status == CorrespondenceStatusExt.Published);
        Assert.Equal(CorrespondenceStatusExt.Published, response.Status);
    }

    [Fact]
    public async Task GetCorrespondenceDetails_AsSender_KeepsStatusAsPublished()
    {
        // Arrange
        var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
        var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);

        // Act
        var getCorrespondenceDetailsResponse = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.Correspondences.FirstOrDefault().CorrespondenceId}/details");
        Assert.True(getCorrespondenceDetailsResponse.IsSuccessStatusCode, await getCorrespondenceDetailsResponse.Content.ReadAsStringAsync());

        // Assert
        var response = await getCorrespondenceDetailsResponse.Content.ReadFromJsonAsync<CorrespondenceDetailsExt>(_responseSerializerOptions);
        Assert.DoesNotContain(response.StatusHistory, item => item.Status == CorrespondenceStatusExt.Fetched);
        Assert.Equal(CorrespondenceStatusExt.Published, response.Status);
    }

    [Fact]
    public async Task UpdateCorrespondenceStatus_CorrespondenceNotExists_Return404()
    {
        var readResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/00000000-0100-0000-0000-000000000000/markasread", null);
        Assert.Equal(HttpStatusCode.NotFound, readResponse.StatusCode);

        var confirmResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/00000000-0100-0000-0000-000000000000/confirm", null);
        Assert.Equal(HttpStatusCode.NotFound, confirmResponse.StatusCode);

        var archiveResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/00000000-0100-0000-0000-000000000000/archive", null);
        Assert.Equal(HttpStatusCode.NotFound, archiveResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateCorrespondenceStatus_CorrespondenceNotPublished_Return404()
    {
        // Arrange
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(1))
            .Build();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        var correspondenceResponse = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        Assert.NotNull(correspondenceResponse);
        var correspondenceId = correspondenceResponse?.Correspondences.FirstOrDefault()?.CorrespondenceId;

        // Act and Assert
        var readResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/{correspondenceId}/markasread", null);
        Assert.Equal(HttpStatusCode.NotFound, readResponse.StatusCode);

        // Act and Assert
        var confirmResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/{correspondenceId}/confirm", null);
        Assert.Equal(HttpStatusCode.NotFound, confirmResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateCorrespondenceStatus_CorrespondencePublished_ReturnOk()
    {
        // Arrange
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .Build();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
        var response = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
        Assert.Equal(CorrespondenceStatusExt.Published, response?.Correspondences.FirstOrDefault()?.Status);
        var correspondenceId = response?.Correspondences.FirstOrDefault()?.CorrespondenceId;

        // Act
        var fetchResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}"); // Fetch in order to be able to Read
        Assert.Equal(HttpStatusCode.OK, fetchResponse.StatusCode);

        var readResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/{correspondenceId}/markasread", null);
        readResponse.EnsureSuccessStatusCode();

        // Assert
        var overview = await _senderClient.GetFromJsonAsync<CorrespondenceOverviewExt>($"correspondence/api/v1/correspondence/{correspondenceId}", _responseSerializerOptions);
        Assert.Equal(CorrespondenceStatusExt.Read, overview?.Status);
    }
    
    [Fact]
    public async Task UpdateCorrespondenceStatus_MarkAsRead_WithoutFetched_ReturnsBadRequest()
    {
        //  Arrange
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .Build();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        var correspondenceResponse = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        Assert.Equal(CorrespondenceStatusExt.Published, correspondenceResponse?.Correspondences?.FirstOrDefault()?.Status);
        var correspondenceId = correspondenceResponse?.Correspondences?.FirstOrDefault()?.CorrespondenceId;

        //  Act
        var confirmResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/{correspondenceId}/markasread", null);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, confirmResponse.StatusCode);
    }
    
    [Fact]
    public async Task UpdateCorrespondenceStatus_ToConfirmed_WithoutFetched_ReturnsBadRequest()
    {
        //  Arrange
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .Build();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        var correspondenceResponse = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        Assert.Equal(CorrespondenceStatusExt.Published, correspondenceResponse?.Correspondences?.FirstOrDefault()?.Status);
        var correspondenceId = correspondenceResponse?.Correspondences?.FirstOrDefault()?.CorrespondenceId;

        //  Act
        var confirmResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/{correspondenceId}/confirm", null);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, confirmResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateCorrespondenceStatus_ToConfirmed_WhenCorrespondenceIsFetched_GivesOk()
    {
        //  Arrange
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .Build();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        var correspondenceResponse = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        Assert.Equal(CorrespondenceStatusExt.Published, correspondenceResponse?.Correspondences?.FirstOrDefault()?.Status);
        var correspondenceId = correspondenceResponse?.Correspondences?.FirstOrDefault()?.CorrespondenceId;

        //  Act
        var fetchResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}");
        Assert.Equal(HttpStatusCode.OK, fetchResponse.StatusCode);
        var confirmResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/{correspondenceId}/confirm", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateCorrespondenceStatus_ToArchived_WithoutConfirmation_WhenConfirmationNeeded_ReturnsBadRequest()
    {
        //  Arrange
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithDueDateTime(DateTimeOffset.UtcNow.AddDays(1))
            .WithConfirmationNeeded(true)
            .Build();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        var correspondenceResponse = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        Assert.Equal(CorrespondenceStatusExt.Published, correspondenceResponse?.Correspondences?.FirstOrDefault()?.Status);
        var correspondenceId = correspondenceResponse?.Correspondences?.FirstOrDefault()?.CorrespondenceId;

        //  Act
        var archiveResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/{correspondenceId}/archive", null);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, archiveResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateCorrespondenceStatus_ToArchived_WithConfirmation_WhenConfirmationNeeded_GivesOk()
    {
        //  Arrange
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithDueDateTime(DateTimeOffset.UtcNow.AddDays(1))
            .WithConfirmationNeeded(true)
            .Build();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        var correspondenceResponse = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        Assert.Equal(CorrespondenceStatusExt.Published, correspondenceResponse?.Correspondences?.FirstOrDefault()?.Status);
        var correspondenceId = correspondenceResponse?.Correspondences?.FirstOrDefault()?.CorrespondenceId;

        //  Act
        var fetchResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}"); // Fetch in order to be able to Confirm
        Assert.Equal(HttpStatusCode.OK, fetchResponse.StatusCode);
        var confirmResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/{correspondenceId}/confirm", null); // Update to Confirmed in order to be able to Archive
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        var archiveResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/{correspondenceId}/archive", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);
    }

    [Fact]
    public async Task Correspondence_with_dataLocationUrl_Reuses_Attachment()
    {
        var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithExistingAttachments([attachmentId])
            .WithAttachments([])
            .Build();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
        var response = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
        Assert.Equal(attachmentId, response?.AttachmentIds?.FirstOrDefault());
    }

    [Fact]
    public async Task DownloadCorrespondenceAttachment_AsRecipient_Succeeds()
    {
        // Arrange
        var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithExistingAttachments([attachmentId])
            .Build();

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
        var response = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        var downloadResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{response?.Correspondences.FirstOrDefault().CorrespondenceId}/attachment/{attachmentId}/download");
        var data = downloadResponse.Content.ReadAsByteArrayAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        Assert.NotNull(data);
    }

    [Fact]
    public async Task DownloadCorrespondenceAttachment_WhenNotARecipient_Returns404()
    {
        // Arrange
        var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithExistingAttachments([attachmentId])
            .WithRecipients(["0192:999999999"]) // Change recipient to invalid org
            .Build();

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
        var response = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        var downloadResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{response?.Correspondences.FirstOrDefault().CorrespondenceId}/attachment/{attachmentId}/download");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, downloadResponse.StatusCode);
    }

    [Fact]
    public async Task DownloadCorrespondenceAttachment_WhenCorrespondenceUnavailable_Returns404()
    {
        // Arrange
        var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithExistingAttachments([attachmentId])
            .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(1)) // Set RequestedPublishTime in the future so that it is not published
            .Build();


        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
        var response = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        var downloadResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{response?.Correspondences.FirstOrDefault().CorrespondenceId}/attachment/{attachmentId}/download");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, downloadResponse.StatusCode);
    }

    [Fact]
    public async Task DownloadCorrespondenceAttachment_WhenCorrespondenceHasNoAttachment_Returns404()
    {
        // Arrange
        var attachmentId = Guid.NewGuid().ToString();
        var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
        var response = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        var downloadResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{response?.Correspondences.FirstOrDefault().CorrespondenceId}/attachment/{attachmentId}/download");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, downloadResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_ReadyForPublished_Correspondence_SuccessForSender_FailsForRecipient()
    {
        // Arrange
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(1))
            .Build();

        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        var correspondenceResponse = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        Assert.NotNull(correspondenceResponse);

        // Act (Call recipient first to ensure that the correspondence is not purged)
        var recipientResponse = await _recipientClient.DeleteAsync($"correspondence/api/v1/correspondence/{correspondenceResponse.Correspondences.FirstOrDefault().CorrespondenceId}/purge");
        var senderResponse = await _senderClient.DeleteAsync($"correspondence/api/v1/correspondence/{correspondenceResponse.Correspondences.FirstOrDefault().CorrespondenceId}/purge");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, recipientResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, senderResponse.StatusCode);
        var overview = await _senderClient.GetFromJsonAsync<CorrespondenceOverviewExt>($"correspondence/api/v1/correspondence/{correspondenceResponse.Correspondences.FirstOrDefault().CorrespondenceId}", _responseSerializerOptions);
        Assert.Equal(overview?.Status, CorrespondenceStatusExt.PurgedByAltinn);
    }

    [Fact]
    public async Task Delete_Published_Correspondence_SuccessForRecipient_FailsForSender()
    {
        // Arrange
        var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();

        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        var correspondenceResponse = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        Assert.NotNull(correspondenceResponse);

        // Act (Call sender first to ensure that the correspondence is not purged)
        var senderResponse = await _senderClient.DeleteAsync($"correspondence/api/v1/correspondence/{correspondenceResponse.Correspondences.FirstOrDefault().CorrespondenceId}/purge");
        var recipientResponse = await _recipientClient.DeleteAsync($"correspondence/api/v1/correspondence/{correspondenceResponse.Correspondences.FirstOrDefault().CorrespondenceId}/purge");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, senderResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, recipientResponse.StatusCode);
        var overview = await _senderClient.GetFromJsonAsync<CorrespondenceOverviewExt>($"correspondence/api/v1/correspondence/{correspondenceResponse.Correspondences.FirstOrDefault().CorrespondenceId}", _responseSerializerOptions);
        Assert.Equal(overview?.Status, CorrespondenceStatusExt.PurgedByRecipient);
    }

    [Fact]
    public async Task Delete_Published_Correspondence_WithoutConfirmation_WhenConfirmationNeeded_ReturnsBadRequest()
    {
        //  Arrange
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithDueDateTime(DateTimeOffset.UtcNow.AddDays(1))
            .WithConfirmationNeeded(true)
            .Build();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        var correspondenceResponse = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        Assert.Equal(CorrespondenceStatusExt.Published, correspondenceResponse?.Correspondences?.FirstOrDefault()?.Status);
        var correspondenceId = correspondenceResponse?.Correspondences?.FirstOrDefault()?.CorrespondenceId;

        //  Act
        var deleteResponse = await _recipientClient.DeleteAsync($"correspondence/api/v1/correspondence/{correspondenceId}/purge");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_Published_Correspondence_WithConfirmation_WhenConfirmationNeeded_Gives_OK()
    {
        //  Arrange
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithDueDateTime(DateTimeOffset.UtcNow.AddDays(1))
            .WithConfirmationNeeded(true)
            .Build();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        var correspondenceResponse = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        Assert.Equal(CorrespondenceStatusExt.Published, correspondenceResponse?.Correspondences?.FirstOrDefault()?.Status);
        var correspondenceId = correspondenceResponse?.Correspondences?.FirstOrDefault()?.CorrespondenceId;

        //  Act
        var fetchResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}"); // Fetch in order to be able to confirm
        Assert.Equal(HttpStatusCode.OK, fetchResponse.StatusCode);
        var confirmResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/{correspondenceId}/confirm", null); // Confirm in order to be able to delete
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        var deleteResponse = await _recipientClient.DeleteAsync($"correspondence/api/v1/correspondence/{correspondenceId}/purge");

        // Assert
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_Correspondence_Also_deletes_attachment()
    {
        // Arrange
        var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithExistingAttachments([attachmentId])
            .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(1))
            .Build();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        var correspondenceResponse = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        Assert.NotNull(correspondenceResponse);

        // Act
        foreach (var correspondence in correspondenceResponse.Correspondences)
        {
            var correspondenceId = correspondence.CorrespondenceId;
            var response = await _senderClient.DeleteAsync($"correspondence/api/v1/correspondence/{correspondenceId}/purge");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var overview = await _senderClient.GetFromJsonAsync<CorrespondenceOverviewExt>($"correspondence/api/v1/correspondence/{correspondenceId}", _responseSerializerOptions);
            Assert.Equal(overview?.Status, CorrespondenceStatusExt.PurgedByAltinn);
        }

        // Assert
        var attachment = await _senderClient.GetFromJsonAsync<AttachmentOverviewExt>($"correspondence/api/v1/attachment/{correspondenceResponse.AttachmentIds.FirstOrDefault()}", _responseSerializerOptions);
        Assert.Equal(attachment?.Status, AttachmentStatusExt.Purged);
    }

    [Fact]
    public async Task Delete_correspondence_dont_delete_attachment_with_multiple_correspondences()
    {
        // Arrange
        var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
        var payload1 = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithExistingAttachments([attachmentId])
            .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(1))
            .Build();
        var payload2 = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithExistingAttachments([attachmentId])
            .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(1))
            .Build();

        var initializeCorrespondenceResponse1 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload1, _responseSerializerOptions);
        var response1 = await initializeCorrespondenceResponse1.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        initializeCorrespondenceResponse1.EnsureSuccessStatusCode();
        Assert.NotNull(response1);

        var initializeCorrespondenceResponse2 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload2, _responseSerializerOptions);
        var response2 = await initializeCorrespondenceResponse2.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        initializeCorrespondenceResponse2.EnsureSuccessStatusCode();
        Assert.NotNull(response2);

        var deleteResponse = await _senderClient.DeleteAsync($"correspondence/api/v1/correspondence/{response1.Correspondences.FirstOrDefault().CorrespondenceId}/purge");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var attachmentOverview = await _senderClient.GetFromJsonAsync<AttachmentOverviewExt>($"correspondence/api/v1/attachment/{response1.AttachmentIds.FirstOrDefault()}", _responseSerializerOptions);
        Assert.NotEqual(attachmentOverview?.Status, AttachmentStatusExt.Purged);
    }

    [Fact]
    public async Task Delete_NonExisting_Correspondence_Gives_NotFound()
    {
        var deleteResponse = await _senderClient.DeleteAsync($"correspondence/api/v1/correspondence/00000000-0100-0000-0000-000000000000/purge");
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task CorrespondenceWithGenericNotification_Gives_Ok()
    {
        var smsPayload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
            .WithNotificationChannel(NotificationChannelExt.Sms)
            .WithEmailContent()
            .Build();
        var initializeCorrespondenceResponse1 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", smsPayload, _responseSerializerOptions);
        var response1 = await initializeCorrespondenceResponse1.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        initializeCorrespondenceResponse1.EnsureSuccessStatusCode();
        Assert.NotNull(response1);

        var emailPayload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
            .WithNotificationChannel(NotificationChannelExt.Email)
            .WithEmailContent()
            .Build();
        var initializeCorrespondenceResponse2 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", emailPayload, _responseSerializerOptions);
        var response2 = await initializeCorrespondenceResponse2.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        initializeCorrespondenceResponse2.EnsureSuccessStatusCode();
        Assert.NotNull(response2);

        var emptySmsPayload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
            .WithNotificationChannel(NotificationChannelExt.Sms)
            .Build();
        var initializeCorrespondenceResponse3 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", emptySmsPayload, _responseSerializerOptions);
        var response3 = await initializeCorrespondenceResponse3.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        initializeCorrespondenceResponse3.EnsureSuccessStatusCode();
        Assert.NotNull(response3);

        var emptyEmailPayload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
            .WithNotificationChannel(NotificationChannelExt.Email)
            .Build();
        var initializeCorrespondenceResponse4 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", emptyEmailPayload, _responseSerializerOptions);
        var response4 = await initializeCorrespondenceResponse4.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        initializeCorrespondenceResponse4.EnsureSuccessStatusCode();
        Assert.NotNull(response4);
    }

    [Fact]
    public async Task CorrespondenceWithCustomNotification_Gives_Ok()
    {
        var emailPayload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithNotificationTemplate(NotificationTemplateExt.CustomMessage)
            .WithNotificationChannel(NotificationChannelExt.Email)
            .WithEmailContent()
            .WithEmailReminder()
            .Build();
        var initializeCorrespondenceResponse1 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", emailPayload, _responseSerializerOptions);
        var response1 = await initializeCorrespondenceResponse1.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        initializeCorrespondenceResponse1.EnsureSuccessStatusCode();
        Assert.NotNull(response1);

        var smsPayload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithNotificationTemplate(NotificationTemplateExt.CustomMessage)
            .WithNotificationChannel(NotificationChannelExt.Sms)
            .WithSmsContent()
            .WithSmsReminder()
            .Build();
        var initializeCorrespondenceResponse2 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", smsPayload, _responseSerializerOptions);
        var response2 = await initializeCorrespondenceResponse2.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        initializeCorrespondenceResponse2.EnsureSuccessStatusCode();
        Assert.NotNull(response2);
    }

    [Fact]
    public async Task CorrespondenceWithPrefferedNotification_Gives_Ok()
    {
        var preferredEmailCustomPayload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithNotificationTemplate(NotificationTemplateExt.CustomMessage)
            .WithNotificationChannel(NotificationChannelExt.EmailPreferred)
            .WithEmailContent()
            .WithSmsContent()
            .WithEmailReminder()
            .WithSmsReminder()
            .Build();
        var initializeCorrespondenceResponse1 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", preferredEmailCustomPayload, _responseSerializerOptions);
        var response1 = await initializeCorrespondenceResponse1.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        initializeCorrespondenceResponse1.EnsureSuccessStatusCode();
        Assert.NotNull(response1);

        var emailPreferredAltinnPayload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
            .WithNotificationChannel(NotificationChannelExt.EmailPreferred)
            .Build();
        var initializeCorrespondenceResponse2 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", emailPreferredAltinnPayload, _responseSerializerOptions);
        var response2 = await initializeCorrespondenceResponse2.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        initializeCorrespondenceResponse2.EnsureSuccessStatusCode();
        Assert.NotNull(response2);
    }

    [Fact]
    public async Task CorrespondenceWithEmailNotificationAndSmsReminder_Gives_Ok()
    {
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
            .WithNotificationChannel(NotificationChannelExt.Email)
            .WithReminderNotificationChannel(NotificationChannelExt.Sms)
            .WithEmailContent()
            .WithSmsReminder()
            .Build();
        var initializeCorrespondenceResponse1 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
        var response1 = await initializeCorrespondenceResponse1.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        initializeCorrespondenceResponse1.EnsureSuccessStatusCode();
        Assert.NotNull(response1);

        var payload2 = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
            .WithNotificationChannel(NotificationChannelExt.Email)
            .WithReminderNotificationChannel(NotificationChannelExt.Sms)
            .WithEmailContent()
            .WithSmsReminder()
            .WithEmailReminder()
            .Build();
        var initializeCorrespondenceResponse2 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload2, _responseSerializerOptions);
        var response2 = await initializeCorrespondenceResponse2.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        initializeCorrespondenceResponse2.EnsureSuccessStatusCode();
        Assert.NotNull(response2);
    }

    [Fact]
    public async Task CorrespondenceWithSmsNotificationAndEmailReminder_Gives_Ok()
    {
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
            .WithNotificationChannel(NotificationChannelExt.Sms)
            .WithReminderNotificationChannel(NotificationChannelExt.Email)
            .WithSmsReminder()
            .WithEmailReminder()
            .Build();

        var initializeCorrespondenceResponse1 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
        var response1 = await initializeCorrespondenceResponse1.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        initializeCorrespondenceResponse1.EnsureSuccessStatusCode();
        Assert.NotNull(response1);

        var payload2 = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
            .WithNotificationChannel(NotificationChannelExt.Sms)
            .WithReminderNotificationChannel(NotificationChannelExt.EmailPreferred)
            .WithSmsReminder()
            .WithEmailReminder()
            .Build();
        var initializeCorrespondenceResponse2 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload2, _responseSerializerOptions);
        var response2 = await initializeCorrespondenceResponse2.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        initializeCorrespondenceResponse2.EnsureSuccessStatusCode();
        Assert.NotNull(response2);
    }

    [Fact]
    public async Task CorrespondenceWithEmptyCustomNotification_Gives_BadRequest()
    {
        var payload1 = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithNotificationTemplate(NotificationTemplateExt.CustomMessage)
            .WithNotificationChannel(NotificationChannelExt.Email)
            .Build();
        var initializeCorrespondenceResponse1 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload1, _responseSerializerOptions);
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse1.StatusCode);

        var payload2 = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithNotificationTemplate(NotificationTemplateExt.CustomMessage)
            .WithNotificationChannel(NotificationChannelExt.Sms)
            .Build();
        var initializeCorrespondenceResponse2 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload2, _responseSerializerOptions);
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse2.StatusCode);

        var payload3 = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithNotificationTemplate(NotificationTemplateExt.CustomMessage)
            .WithNotificationChannel(NotificationChannelExt.Email)
            .WithoutSendReminder()
            .Build();
        var initializeCorrespondenceResponse3 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload3, _responseSerializerOptions);
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse3.StatusCode);

        var payload4 = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithNotificationTemplate(NotificationTemplateExt.CustomMessage)
            .WithNotificationChannel(NotificationChannelExt.Email)
            .WithEmailContent()
            .Build();
        var initializeCorrespondenceResponse4 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload4, _responseSerializerOptions);
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse4.StatusCode);
    }
    [Fact]
    public async Task Correspondence_WithNotification_Failed_Returns_MissingContact()
    {
        // Arrange
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
            .WithNotificationChannel(NotificationChannelExt.Email)
            .Build();
        var orderId = Guid.NewGuid();

        var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
        {
            var hangfireBackgroundJobClient = new Mock<IBackgroundJobClient>();
            hangfireBackgroundJobClient.Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>())).Returns("1");
            services.AddSingleton(hangfireBackgroundJobClient.Object);
            var mockNotificationService = new Mock<IAltinnNotificationService>();
            mockNotificationService.Setup(x => x.CreateNotification(It.IsAny<NotificationOrderRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new NotificationOrderRequestResponse()
            {
                OrderId = orderId,
                RecipientLookup = new RecipientLookupResult()
                {
                    Status = RecipientLookupStatus.Failed,
                    MissingContact = [],
                    IsReserved = []
                }
            });
            services.AddSingleton(mockNotificationService.Object);
        });
        var senderClient = testFactory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.SenderScope));

        // Act
        var initializeCorrespondenceResponse = await senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
        var content = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, initializeCorrespondenceResponse.StatusCode);
        Assert.Equal(CorrespondenceStatusExt.Published, content?.Correspondences.First().Status);
        Assert.Equal(InitializedNotificationStatusExt.MissingContact, content?.Correspondences?.First()?.Notifications?.First().Status);
        Assert.Equal(orderId, content?.Correspondences?.First()?.Notifications?.First().OrderId);
    }
    [Fact]
    public async Task Correspondence_WithNotification_Failed_Returns_Failed()
    {
        // Arrange
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
            .WithNotificationChannel(NotificationChannelExt.Email)
            .Build();

        var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
        {
            var hangfireBackgroundJobClient = new Mock<IBackgroundJobClient>();
            hangfireBackgroundJobClient.Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>())).Returns("1");
            services.AddSingleton(hangfireBackgroundJobClient.Object);
            var mockNotificationService = new Mock<IAltinnNotificationService>();
            mockNotificationService.Setup(x => x.CreateNotification(It.IsAny<NotificationOrderRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync((NotificationOrderRequestResponse)null);
            services.AddSingleton(mockNotificationService.Object);
        });
        var senderClient = testFactory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.SenderScope));

        // Act
        var initializeCorrespondenceResponse = await senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
        var content = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, initializeCorrespondenceResponse.StatusCode);
        Assert.Equal(CorrespondenceStatusExt.Published, content?.Correspondences.First().Status);
        Assert.Equal(InitializedNotificationStatusExt.Failure, content?.Correspondences?.First()?.Notifications?.First().Status);
        Assert.Equal(Guid.Empty, content?.Correspondences?.First()?.Notifications?.First().OrderId);
    }
    [Fact]
    public async Task Correspondence_WithNotification_Success_Returns_OK()
    {
        // Arrange
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
            .WithNotificationChannel(NotificationChannelExt.Email)
            .Build();
        var orderId = Guid.NewGuid();

        var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
        {
            var hangfireBackgroundJobClient = new Mock<IBackgroundJobClient>();
            hangfireBackgroundJobClient.Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>())).Returns("1");
            services.AddSingleton(hangfireBackgroundJobClient.Object);
            var mockNotificationService = new Mock<IAltinnNotificationService>();
            mockNotificationService.Setup(x => x.CreateNotification(It.IsAny<NotificationOrderRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new NotificationOrderRequestResponse()
            {
                OrderId = orderId,
                RecipientLookup = new RecipientLookupResult()
                {
                    Status = RecipientLookupStatus.Success,
                    MissingContact = [],
                    IsReserved = []
                }
            });
            services.AddSingleton(mockNotificationService.Object);
        });
        var senderClient = testFactory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.SenderScope));

        // Act
        var initializeCorrespondenceResponse = await senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
        var content = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, initializeCorrespondenceResponse.StatusCode);
        Assert.Equal(CorrespondenceStatusExt.Published, content?.Correspondences.First().Status);
        Assert.Equal(InitializedNotificationStatusExt.Success, content?.Correspondences?.First()?.Notifications?.First().Status);
        Assert.Equal(orderId, content?.Correspondences?.First()?.Notifications?.First().OrderId);
    }

    [Fact]
    public async Task CancelNotificationHandler_SendsSlackNotification_WhenCancellationJobFailsWithMaximumRetries()
    {
        // Arrange
        var correspondence = CorrespondenceBuilder.CorrespondenceEntityWithNotifications();
        var loggerMock = new Mock<ILogger<CancelNotificationHandler>>();
        var correspondenceRepositoryMock = new Mock<ICorrespondenceRepository>();
        var altinnNotificationServiceMock = new Mock<IAltinnNotificationService>();
        var slackClientMock = new Mock<ISlackClient>();
        var backgroundJobClient = new Mock<IBackgroundJobClient>();
        var hostEnvironment = new Mock<IHostEnvironment>();

        var cancelNotificationHandler = new CancelNotificationHandler(loggerMock.Object, correspondenceRepositoryMock.Object, altinnNotificationServiceMock.Object, slackClientMock.Object, backgroundJobClient.Object, hostEnvironment.Object);
        var notificationEntities = correspondence.Notifications;
        notificationEntities.ForEach(notification =>
        {
            notification.RequestedSendTime = correspondence.RequestedPublishTime.AddMinutes(1); // Set requested send time to future
            notification.NotificationOrderId = null; // Invalidate notification order id
        });

        // Act
        try
        {
            await cancelNotificationHandler.CancelNotification(Guid.Empty, notificationEntities, retryAttempts: 10, default);
        }
        catch
        {
            Console.WriteLine("Exception thrown");
        }

        // Assert
        slackClientMock.Verify(client => client.Post(It.IsAny<SlackMessage>()), Times.Once);
    }

    private MultipartFormDataContent CorrespondenceToFormData(BaseCorrespondenceExt correspondence)
    {
        var formData = new MultipartFormDataContent(){
            { new StringContent(correspondence.ResourceId), "correspondence.resourceId" },
            { new StringContent(correspondence.Sender), "correspondence.sender" },
            { new StringContent(correspondence.SendersReference), "correspondence.sendersReference" },
            { new StringContent(correspondence.RequestedPublishTime.ToString()), "correspondence.RequestedPublishTime" },
            { new StringContent(correspondence.DueDateTime.ToString()), "correspondence.dueDateTime" },
            { new StringContent(correspondence.AllowSystemDeleteAfter.ToString()), "correspondence.AllowSystemDeleteAfter" },
            { new StringContent(correspondence.Content.MessageTitle), "correspondence.content.MessageTitle" },
            { new StringContent(correspondence.Content.MessageSummary), "correspondence.content.MessageSummary" },
            { new StringContent(correspondence.Content.MessageBody), "correspondence.content.MessageBody" },
            { new StringContent(correspondence.Content.Language), "correspondence.content.Language" },
            { new StringContent((correspondence.IgnoreReservation ?? false).ToString()), "correspondence.IgnoreReservation" },
        };
        if (correspondence.Notification != null)
        {
            formData.Add(new StringContent(correspondence.Notification.NotificationTemplate.ToString()), "correspondence.Notification.NotificationTemplate");
            formData.Add(new StringContent(correspondence.Notification.SendReminder.ToString()), "correspondence.Notification.SendReminder");
            if (correspondence.Notification.RequestedSendTime != null) formData.Add(new StringContent(correspondence.Notification.RequestedSendTime.ToString()), "correspondence.Notification.RequestedSendTime");
            if (correspondence.Notification.EmailBody != null) formData.Add(new StringContent(correspondence.Notification.EmailBody), "correspondence.Notification.EmailBody");
            if (correspondence.Notification.EmailSubject != null) formData.Add(new StringContent(correspondence.Notification.EmailSubject), "correspondence.Notification.EmailSubject");
            if (correspondence.Notification.ReminderEmailBody != null) formData.Add(new StringContent(correspondence.Notification.ReminderEmailBody), "correspondence.Notification.ReminderEmailBody");
            if (correspondence.Notification.ReminderEmailSubject != null) formData.Add(new StringContent(correspondence.Notification.ReminderEmailSubject), "correspondence.Notification.ReminderEmailSubject");
            if (correspondence.Notification.SmsBody != null) formData.Add(new StringContent(correspondence.Notification.SmsBody), "correspondence.Notification.SmsBody");
            if (correspondence.Notification.ReminderSmsBody != null) formData.Add(new StringContent(correspondence.Notification.ReminderSmsBody), "correspondence.Notification.ReminderSmsBody");
        }

        correspondence.Content.Attachments.Select((attachment, index) => new[]
        {
            new { Key = $"correspondence.content.Attachments[{index}].DataLocationType", Value = attachment.DataLocationType.ToString() },
            new { Key = $"correspondence.content.Attachments[{index}].DataType", Value = attachment.DataType },
            new { Key = $"correspondence.content.Attachments[{index}].Name", Value = attachment.Name },
            new { Key = $"correspondence.content.Attachments[{index}].FileName", Value = attachment.FileName ?? "" },
            new { Key = $"correspondence.content.Attachments[{index}].SendersReference", Value = attachment.SendersReference },
            new { Key = $"correspondence.content.Attachments[{index}].IsEncrypted", Value = attachment.IsEncrypted.ToString() }
        }).SelectMany(x => x).ToList()
        .ForEach(item => formData.Add(new StringContent(item.Value), item.Key));

        correspondence.ExternalReferences?.Select((externalReference, index) => new[]
        {
            new { Key = $"correspondence.ExternalReference[{index}].ReferenceType", Value = externalReference.ReferenceType.ToString() },
            new { Key = $"correspondence.ExternalReference[{index}].ReferenceValue", Value = externalReference.ReferenceValue },
        }).SelectMany(x => x).ToList()
        .ForEach(item => formData.Add(new StringContent(item.Value), item.Key));

        correspondence.ReplyOptions.Select((replyOption, index) => new[]
        {
            new { Key = $"correspondence.ReplyOptions[{index}].LinkURL", Value = replyOption.LinkURL },
            new { Key = $"correspondence.ReplyOptions[{index}].LinkText", Value = replyOption.LinkText ?? "" }
        }).SelectMany(x => x).ToList()
        .ForEach(item => formData.Add(new StringContent(item.Value), item.Key));

        correspondence.PropertyList.ToList()
        .ForEach((item) => formData.Add(new StringContent(item.Value), "correspondence.propertyLists." + item.Key));
        return formData;
    }
}
