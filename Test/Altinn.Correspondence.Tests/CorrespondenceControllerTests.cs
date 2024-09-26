using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.GetCorrespondences;
using Altinn.Correspondence.Application.Configuration;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Data;

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
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondences());
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task InitializeCorrespondence_With_HTML_Or_Markdown_In_Title_fails()
    {
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondenceWithHtmlInTitle());
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);

        initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondenceWithMarkdownInTitle());
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
    }
    [Fact]
    public async Task InitializeCorrespondence_With_HTML_In_Summary_Or_Body_fails()
    {
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondenceWithHtmlInSummary());
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);

        initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondenceWithHtmlInBody());
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
    }
    [Fact]
    public async Task InitializeCorrespondence_No_Message_Body_fails()
    {
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondenceWithNoMessageBody());
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
    }
    [Fact]
    public async Task InitializeCorrespondence_With_Different_Markdown_In_Body()
    {
        var payload = InitializeCorrespondenceFactory.BasicCorrespondences();
        payload.Correspondence.Content.MessageBody = File.ReadAllText("Data/Markdown.text");
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task InitializeCorrespondence_Recipient_Can_Handle_Org_And_Ssn()
    {
        var payload = InitializeCorrespondenceFactory.BasicCorrespondences();
        payload.Recipients = new List<string> { "1234:123456789" };
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();

        payload.Recipients = new List<string> { "12345678901" };
        initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task InitializeCorrespondence_With_Invalid_Sender_Returns_BadRequest()
    {
        var payload = InitializeCorrespondenceFactory.BasicCorrespondences();
        payload.Correspondence.Sender = "invalid-sender";
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);

        payload.Correspondence.Sender = "123456789";
        initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task InitializeCorrespondence_With_Invalid_Recipient_Returns_BadRequest()
    {
        var payload = InitializeCorrespondenceFactory.BasicCorrespondences();
        payload.Recipients = new List<string> { "invalid-recipient" };
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);

        payload.Recipients = new List<string> { "123456789" };
        initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);

        payload.Recipients = new List<string> { "1234567812390123" };
        initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task InitializeCorrespondence_As_Recipient_Is_Forbidden()
    {
        var payload = InitializeCorrespondenceFactory.BasicCorrespondences();
        var initializeCorrespondenceResponse = await _recipientClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        Assert.Equal(HttpStatusCode.Forbidden, initializeCorrespondenceResponse.StatusCode);
    }
    [Fact]
    public async Task InitializeCorrespondence_DueDate_PriorToday_Returns_BadRequest()
    {
        // Arrange
        var correspondence = InitializeCorrespondenceFactory.BasicCorrespondences();
        correspondence.Correspondence.DueDateTime = DateTimeOffset.Now.AddDays(-7);

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
    }
    [Fact]
    public async Task InitializeCorrespondence_DueDate_PriorVisibleFrom_Returns_BadRequest()
    {
        // Arrange
        var correspondence = InitializeCorrespondenceFactory.BasicCorrespondences();
        correspondence.Correspondence.DueDateTime = DateTimeOffset.Now.AddDays(7);
        correspondence.Correspondence.VisibleFrom = DateTimeOffset.Now.AddDays(14);

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task InitializeCorrespondence_AllowSystemDeleteAfter_PriorToday_Returns_BadRequest()
    {
        // Arrange
        var correspondence = InitializeCorrespondenceFactory.BasicCorrespondences();
        correspondence.Correspondence.AllowSystemDeleteAfter = DateTimeOffset.Now.AddDays(-7);

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task InitializeCorrespondence_AllowSystemDeleteAfter_PriorVisibleFrom_Returns_BadRequest()
    {
        // Arrange
        var correspondence = InitializeCorrespondenceFactory.BasicCorrespondences();
        correspondence.Correspondence.VisibleFrom = DateTimeOffset.Now.AddDays(14);
        correspondence.Correspondence.DueDateTime = DateTimeOffset.Now.AddDays(21); // ensure DueDate is after VisibleFrom
        correspondence.Correspondence.AllowSystemDeleteAfter = DateTimeOffset.Now.AddDays(7);

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
    }
    [Fact]
    public async Task InitializeCorrespondence_AllowSystemDeleteAfter_PriorDueDate_Returns_BadRequest()
    {
        // Arrange
        var correspondence = InitializeCorrespondenceFactory.BasicCorrespondences();
        correspondence.Correspondence.VisibleFrom = DateTimeOffset.Now.AddDays(7);
        correspondence.Correspondence.AllowSystemDeleteAfter = DateTimeOffset.Now.AddDays(14);
        correspondence.Correspondence.DueDateTime = DateTimeOffset.Now.AddDays(21);

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task UploadCorrespondence_Gives_Ok()
    {
        var payload = InitializeCorrespondenceFactory.BasicCorrespondences();
        using (var stream = System.IO.File.OpenRead("./Data/Markdown.text"))
        {
            var file = new FormFile(stream, 0, stream.Length, null, Path.GetFileName(stream.Name));
            var attachmentData = new InitializeCorrespondenceAttachmentExt()
            {
                DataType = "text",
                Name = file.FileName,
                RestrictionName = "testFile3",
                SendersReference = "1234",
                FileName = file.FileName,
                IsEncrypted = false
            };
            payload.Correspondence.Content.Attachments = new List<InitializeCorrespondenceAttachmentExt>() { attachmentData };
            var formData = CorrespondenceToFormData(payload.Correspondence);
            formData.Add(new StringContent("0192:986252932"), "recipients[0]");
            using var fileStream = file.OpenReadStream();
            formData.Add(new StreamContent(fileStream), "attachments", file.FileName);
            var uploadCorrespondenceResponse = await _senderClient.PostAsync("correspondence/api/v1/correspondence/upload", formData);
            Assert.True(uploadCorrespondenceResponse.IsSuccessStatusCode, await uploadCorrespondenceResponse.Content.ReadAsStringAsync());

            var response = await uploadCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            var attachmentId = response?.AttachmentIds.FirstOrDefault();
            var attachmentOverview = await _senderClient.GetFromJsonAsync<AttachmentOverviewExt>($"correspondence/api/v1/attachment/{attachmentId}", _responseSerializerOptions);
            payload.Correspondence.Content.Attachments.Add(new InitializeCorrespondenceAttachmentExt()
            {
                DataType = attachmentOverview.DataType,
                FileName = attachmentOverview.FileName,
                Name = "Logical file name",
                RestrictionName = attachmentOverview.RestrictionName,
                SendersReference = attachmentOverview.SendersReference,
                IsEncrypted = attachmentOverview.IsEncrypted,
                Checksum = attachmentOverview.Checksum
            });
            formData = CorrespondenceToFormData(payload.Correspondence);
            formData.Add(new StreamContent(fileStream), "attachments", file.FileName);
            var uploadCorrespondenceResponse2 = await _senderClient.PostAsync("correspondence/api/v1/correspondence/upload", formData);
            Assert.True(uploadCorrespondenceResponse.IsSuccessStatusCode, await uploadCorrespondenceResponse.Content.ReadAsStringAsync());
        }
    }
    [Fact]
    public async Task UploadCorrespondence_With_Multiple_Files()
    {
        var payload = InitializeCorrespondenceFactory.BasicCorrespondences();

        using var stream = System.IO.File.OpenRead("./Data/Markdown.text");
        var file = new FormFile(stream, 0, stream.Length, null, Path.GetFileName(stream.Name));
        using var fileStream = file.OpenReadStream();
        using var stream2 = System.IO.File.OpenRead("./Data/test.txt");
        var file2 = new FormFile(stream2, 0, stream2.Length, null, Path.GetFileName(stream2.Name));
        using var fileStream2 = file2.OpenReadStream();

        payload.Correspondence.Content.Attachments = new List<InitializeCorrespondenceAttachmentExt>(){
            new InitializeCorrespondenceAttachmentExt(){
                DataType = "text",
                Name = "markdown example",
                RestrictionName = "testFile3",
                SendersReference = "1234",
                FileName = file.FileName,
                IsEncrypted = false,
            },
             new InitializeCorrespondenceAttachmentExt(){
                DataType = "text",
                Name = "test file",
                RestrictionName = "testFile3",
                SendersReference = "1234",
                FileName = file2.FileName,
                IsEncrypted = false,
            }};
        var formData = CorrespondenceToFormData(payload.Correspondence);
        formData.Add(new StringContent("0192:986252932"), "recipients[0]");
        formData.Add(new StreamContent(fileStream), "attachments", file.FileName);
        formData.Add(new StreamContent(fileStream2), "attachments", file2.FileName);

        var uploadCorrespondenceResponse = await _senderClient.PostAsync("correspondence/api/v1/correspondence/upload", formData);
        Assert.True(uploadCorrespondenceResponse.IsSuccessStatusCode, await uploadCorrespondenceResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UploadCorrespondence_No_Files_Gives_Bad_request()
    {
        var payload = InitializeCorrespondenceFactory.BasicCorrespondences();
        payload.Correspondence.Content.Attachments = new List<InitializeCorrespondenceAttachmentExt>() { };
        var formData = CorrespondenceToFormData(payload.Correspondence);
        var uploadCorrespondenceResponse = await _senderClient.PostAsync("correspondence/api/v1/correspondence/upload", formData);
        Assert.Equal(HttpStatusCode.BadRequest, uploadCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task InitializeCorrespondences()
    {
        var uploadedAttachment = await InitializeAttachment();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondences());
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UploadCorrespondences_With_Multiple_Files()
    {

        var payload = InitializeCorrespondenceFactory.BasicCorrespondences();
        using var stream = System.IO.File.OpenRead("./Data/Markdown.text");
        var file = new FormFile(stream, 0, stream.Length, null, Path.GetFileName(stream.Name));
        using var fileStream = file.OpenReadStream();
        using var stream2 = System.IO.File.OpenRead("./Data/test.txt");
        var file2 = new FormFile(stream2, 0, stream2.Length, null, Path.GetFileName(stream2.Name));
        using var fileStream2 = file2.OpenReadStream();
        payload.Correspondence.Content.Attachments = new List<InitializeCorrespondenceAttachmentExt>(){
            new InitializeCorrespondenceAttachmentExt(){
                DataType = "text",
                Name = "MARKDOWN EXAMPLE",
                RestrictionName = "testFile3",
                SendersReference = "1234",
                FileName = file.FileName,
                IsEncrypted = false,
            },
             new InitializeCorrespondenceAttachmentExt(){
                DataType = "text",
                Name = "test file",
                RestrictionName = "testFile3",
                SendersReference = "1234",
                FileName = file2.FileName,
                IsEncrypted = false,
            }};

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
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondences());
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());

        var initializeCorrespondenceResponse2 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondences());
        Assert.True(initializeCorrespondenceResponse2.IsSuccessStatusCode, await initializeCorrespondenceResponse2.Content.ReadAsStringAsync());

        var correspondenceList = await _senderClient.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={1}&offset={0}&limit={10}&status={0}&role={"recipient"}");
        Assert.True(correspondenceList?.Pagination.TotalItems > 0);
    }

    [Fact]
    public async Task GetCorrespondences_WithoutRoleSpecified_ReturnsBadRequest()
    {
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondences());
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());

        var response = await _senderClient.GetAsync($"correspondence/api/v1/correspondence?resourceId={1}&offset={0}&limit={10}&status={0}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetCorrespondencesOnlyFromSearchedResourceId()
    {
        var resourceA = Guid.NewGuid().ToString();
        var resourceB = Guid.NewGuid().ToString();
        var payloadForResourceA = InitializeCorrespondenceFactory.BasicCorrespondences();
        payloadForResourceA.Correspondence.ResourceId = resourceA;
        var payloadForResourceB = InitializeCorrespondenceFactory.BasicCorrespondences();
        payloadForResourceB.Correspondence.ResourceId = resourceB;

        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payloadForResourceA);
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());

        var initializeCorrespondenceResponse2 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payloadForResourceB);
        Assert.True(initializeCorrespondenceResponse2.IsSuccessStatusCode, await initializeCorrespondenceResponse2.Content.ReadAsStringAsync());

        var correspondenceList = await _senderClient.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={resourceA}&offset={0}&limit={10}&status={0}&role={"recipientandsender"}");
        Assert.Equal(correspondenceList?.Pagination.TotalItems, payloadForResourceA.Recipients.Count);
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
        var payload = InitializeCorrespondenceFactory.BasicCorrespondenceWithoutAttachments();
        payload.Correspondence.ResourceId = resource;
        payload.Correspondence.Sender = senderId;
        payload.Recipients = [recipientId, "0192:123456789", "0192:321654987"];
        var senderClient = _factory.CreateClientWithAddedClaims(
            ("consumer", $"{{\"authority\":\"iso6523-actorid-upis\",\"ID\":\"{senderId}\"}}"),
            ("scope", AuthorizationConstants.SenderScope)
        );
        var initResponse = await senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        Assert.True(initResponse.IsSuccessStatusCode, await initResponse.Content.ReadAsStringAsync());

        // Create some correspondences with External sender with senderId and recipientId amongst recipients
        var externalPayload = InitializeCorrespondenceFactory.BasicCorrespondenceWithoutAttachments();
        externalPayload.Correspondence.ResourceId = resource;
        externalPayload.Correspondence.Sender = externalId;
        externalPayload.Recipients = [senderId, recipientId, "0192:864231509"];
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
        var expectedIsSender = payload.Recipients.Count;
        Assert.Equal(expectedIsSender, correspondencesSender?.Pagination.TotalItems);
        var expectedIsRecipient = payload.Recipients.Where(r => r == recipientId).Count() + externalPayload.Recipients.Where(r => r == recipientId).Count();
        Assert.Equal(expectedIsRecipient, correspondencesRecipient?.Pagination.TotalItems);
        var expectedIsSenderAndIsRecipient = expectedIsSender + externalPayload.Recipients.Where(r => r == senderId).Count();
        Assert.Equal(expectedIsSenderAndIsRecipient, correspondencesSenderAndRecipient?.Pagination.TotalItems);
    }

    [Fact]
    public async Task GetCorrespondences_WithStatusSpecified_ShowsSpecifiedCorrespondences()
    {
        // Arrange
        var resourceId = Guid.NewGuid().ToString();
        var initializedCorrespondences = InitializeCorrespondenceFactory.BasicCorrespondences();
        initializedCorrespondences.Correspondence.ResourceId = resourceId;
        await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", initializedCorrespondences);
        var publishedCorrespondences = InitializeCorrespondenceFactory.BasicCorrespondenceWithoutAttachments();
        publishedCorrespondences.Correspondence.ResourceId = resourceId;
        await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", publishedCorrespondences);

        // Act
        var responseWithInitialized = await _senderClient.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={resourceId}&offset=0&limit=10&status={0}&role={"sender"}");
        var responseWithPublished = await _senderClient.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={resourceId}&offset=0&limit=10&status={2}&role={"sender"}");

        // Assert
        Assert.Equal(initializedCorrespondences.Recipients.Count, responseWithInitialized?.Pagination.TotalItems);
        Assert.Equal(publishedCorrespondences.Recipients.Count, responseWithPublished?.Pagination.TotalItems);
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

        var payload = InitializeCorrespondenceFactory.BasicCorrespondenceWithoutAttachments(); // One published
        payload.Correspondence.ResourceId = resource;

        var payloadInitialized = InitializeCorrespondenceFactory.BasicCorrespondences(); // One initialized
        payloadInitialized.Correspondence.ResourceId = resource;

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());
        var initializeCorrespondenceResponse2 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payloadInitialized);
        Assert.True(initializeCorrespondenceResponse2.IsSuccessStatusCode, await initializeCorrespondenceResponse2.Content.ReadAsStringAsync());
        var correspondenceList = await recipientClient.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={resource}&offset=0&limit=10&role={"recipient"}");

        // Assert
        var expected = payload.Recipients.Where(r => r == recipientId).Count(); // Receiver only sees the one that is published
        Assert.Equal(correspondenceList?.Pagination.TotalItems, expected);
    }
    [Fact]
    public async Task GetCorrespondences_WithoutStatusSpecified_AsSender_ReturnsAllExceptBlacklisted()
    {
        // Arrange
        var resource = Guid.NewGuid().ToString();
        var payload = InitializeCorrespondenceFactory.BasicCorrespondences(); // One initialized
        payload.Correspondence.ResourceId = resource;

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());
        var response = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();
        await _senderClient.DeleteAsync($"correspondence/api/v1/correspondence/{response.CorrespondenceIds.FirstOrDefault()}/purge");
        var correspondenceList = await _senderClient.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={resource}&offset=0&limit=10&role={"sender"}");

        // Assert
        var expected = payload.Recipients.Count - 1; // One was deleted
        Assert.Equal(correspondenceList?.Pagination.TotalItems, expected);
    }

    [Fact]
    public async Task GetCorrespondences_WithStatusSpecified_ButStatusIsBlackListed_DoesNotReturnCorrespondence()
    {
        // Arrange
        var resource = Guid.NewGuid().ToString();
        var payload = InitializeCorrespondenceFactory.BasicCorrespondences(); // One initialized
        payload.Correspondence.ResourceId = resource;

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());
        var correspondencesRecipient = await _recipientClient.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={resource}&offset=0&limit=10&status={0}&role={"recipient"}");
        var correspondencesSender = await _senderClient.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={resource}&offset=0&limit=10&status={0}&role={"sender"}");

        // Assert
        var expectedRecipient = 0;
        var expectedSender = payload.Recipients.Count;
        Assert.Equal(correspondencesRecipient?.Pagination.TotalItems, expectedRecipient);
        Assert.Equal(correspondencesSender?.Pagination.TotalItems, expectedSender);
    }

    [Fact]
    public async Task GetCorrespondenceOverview()
    {
        // Arrange
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondences());
        var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();

        // Act
        var getCorrespondenceOverviewResponse = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.CorrespondenceIds.FirstOrDefault()}");
        Assert.True(getCorrespondenceOverviewResponse.IsSuccessStatusCode, await getCorrespondenceOverviewResponse.Content.ReadAsStringAsync());

        // Assert
        var response = await getCorrespondenceOverviewResponse.Content.ReadFromJsonAsync<CorrespondenceOverviewExt>(_responseSerializerOptions);
        Assert.Equal(response.Status, CorrespondenceStatusExt.Initialized);
    }
    [Fact]
    public async Task GetCorrespondenceOverview_WhenNotSenderOrRecipient_Returns404()
    {
        // Arrange
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondences());
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
        var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();
        var invalidSenderClient = _factory.CreateClientWithAddedClaims(("consumer", "{\"authority\":\"iso6523-actorid-upis\",\"ID\":\"0192:123456789\"}"), ("scope", AuthorizationConstants.SenderScope));
        var invalidRecipientClient = _factory.CreateClientWithAddedClaims(("consumer", "{\"authority\":\"iso6523-actorid-upis\",\"ID\":\"0192:123456789\"}"), ("scope", AuthorizationConstants.RecipientScope));

        // Act
        var invalidRecipientResponse = await invalidRecipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.CorrespondenceIds.FirstOrDefault()}");
        var invalidSenderResponse = await invalidSenderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.CorrespondenceIds.FirstOrDefault()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, invalidRecipientResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, invalidSenderResponse.StatusCode);
    }
    [Fact]
    public async Task GetCorrespondenceOverview_AsReceiver_WhenNotPublishedReturns404()
    {
        // Arrange
        var initialCorrespondence = InitializeCorrespondenceFactory.BasicCorrespondences(); // Initialized
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", initialCorrespondence);
        var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();

        // Act
        var getCorrespondenceOverviewResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.CorrespondenceIds.FirstOrDefault()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, getCorrespondenceOverviewResponse.StatusCode);
    }
    [Fact]
    public async Task GetCorrespondenceOverview_AsReceiver_AddsFetchedStatusToHistory()
    {
        // Arrange
        var initialCorrespondence = InitializeCorrespondenceFactory.BasicCorrespondenceWithoutAttachments(); // Published
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", initialCorrespondence);
        var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();

        // Act
        var getCorrespondenceOverviewResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.CorrespondenceIds.FirstOrDefault()}");
        Assert.True(getCorrespondenceOverviewResponse.IsSuccessStatusCode, await getCorrespondenceOverviewResponse.Content.ReadAsStringAsync());

        // Assert
        var response = await getCorrespondenceOverviewResponse.Content.ReadFromJsonAsync<CorrespondenceOverviewExt>(_responseSerializerOptions);
        Assert.Equal(response.Status, CorrespondenceStatusExt.Published); // Status is not changed to fetched
        var actual = await (await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.CorrespondenceIds.FirstOrDefault()}/details")).Content.ReadFromJsonAsync<CorrespondenceDetailsExt>(_responseSerializerOptions);

        var expectedFetchedStatuses = 1;
        Assert.Equal(actual.StatusHistory.Where(s => s.Status == CorrespondenceStatusExt.Fetched).Count(), expectedFetchedStatuses);
        Assert.Contains(actual.StatusHistory, item => item.Status == CorrespondenceStatusExt.Published);
    }
    [Fact]
    public async Task GetCorrespondenceOverview_AsSender_KeepsStatusAsPublished()
    {
        // Arrange
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondenceWithoutAttachments()); // Published
        var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();

        // Act
        var getCorrespondenceOverviewResponse = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.CorrespondenceIds.FirstOrDefault()}");
        Assert.True(getCorrespondenceOverviewResponse.IsSuccessStatusCode, await getCorrespondenceOverviewResponse.Content.ReadAsStringAsync());

        // Assert
        var response = await getCorrespondenceOverviewResponse.Content.ReadFromJsonAsync<CorrespondenceOverviewExt>(_responseSerializerOptions);
        var getCorrespondenceDetailsResponse = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.CorrespondenceIds.FirstOrDefault()}/details");
        var detailsResponse = await getCorrespondenceDetailsResponse.Content.ReadFromJsonAsync<CorrespondenceDetailsExt>(_responseSerializerOptions);
        Assert.DoesNotContain(detailsResponse.StatusHistory, item => item.Status == CorrespondenceStatusExt.Fetched); // Fetched is not added to the list
        Assert.Equal(response.Status, CorrespondenceStatusExt.Published);
    }
    [Fact]
    public async Task GetCorrespondenceDetails()
    {
        // Arrange
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondences());
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
        var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();

        // Act
        var getCorrespondenceDetailsResponse = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.CorrespondenceIds.FirstOrDefault()}/details");
        Assert.True(getCorrespondenceDetailsResponse.IsSuccessStatusCode, await getCorrespondenceDetailsResponse.Content.ReadAsStringAsync());

        // Assert
        var response = await getCorrespondenceDetailsResponse.Content.ReadFromJsonAsync<CorrespondenceDetailsExt>(_responseSerializerOptions);
        Assert.Equal(response.Status, CorrespondenceStatusExt.Initialized);
    }
    [Fact]
    public async Task GetCorrespondenceDetails_WhenNotSenderOrRecipient_Returns404()
    {
        // Arrange
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondences());
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
        var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();
        var invalidSenderClient = _factory.CreateClientWithAddedClaims(("consumer", "{\"authority\":\"iso6523-actorid-upis\",\"ID\":\"0192:123456789\"}"), ("scope", AuthorizationConstants.SenderScope));
        var invalidRecipientClient = _factory.CreateClientWithAddedClaims(("consumer", "{\"authority\":\"iso6523-actorid-upis\",\"ID\":\"0192:123456789\"}"), ("scope", AuthorizationConstants.RecipientScope));

        // Act
        var invalidRecipientResponse = await invalidRecipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.CorrespondenceIds.FirstOrDefault()}/details");
        var invalidSenderResponse = await invalidSenderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.CorrespondenceIds.FirstOrDefault()}/details");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, invalidRecipientResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, invalidSenderResponse.StatusCode);
    }
    [Fact]
    public async Task GetCorrespondenceDetails_AsReceiver_WhenNotPublishedReturns404()
    {
        // Arrange
        var initialCorrespondence = InitializeCorrespondenceFactory.BasicCorrespondences(); // Initialized
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", initialCorrespondence);
        var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();

        // Act
        var getCorrespondenceDetailsResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.CorrespondenceIds.FirstOrDefault()}/details");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, getCorrespondenceDetailsResponse.StatusCode);
    }
    [Fact]
    public async Task GetCorrespondenceDetails_AsReceiver_AddsFetchedStatusToHistory()
    {
        // Arrange
        var initialCorrespondence = InitializeCorrespondenceFactory.BasicCorrespondenceWithoutAttachments();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", initialCorrespondence);
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
        var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();

        // Act
        var getCorrespondenceDetailsResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.CorrespondenceIds.FirstOrDefault()}/details");
        Assert.True(getCorrespondenceDetailsResponse.IsSuccessStatusCode, await getCorrespondenceDetailsResponse.Content.ReadAsStringAsync());

        // Assert
        var response = await getCorrespondenceDetailsResponse.Content.ReadFromJsonAsync<CorrespondenceDetailsExt>(_responseSerializerOptions);
        var expectedFetchedStatuses = 1;
        Assert.Equal(response.StatusHistory.Where(s => s.Status == CorrespondenceStatusExt.Fetched).Count(), expectedFetchedStatuses);
        Assert.Contains(response.StatusHistory, item => item.Status == CorrespondenceStatusExt.Published);
        Assert.Equal(response.Status, CorrespondenceStatusExt.Published);
    }
    [Fact]
    public async Task GetCorrespondenceDetails_AsSender_KeepsStatusAsPublished()
    {
        // Arrange
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondenceWithoutAttachments());
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
        var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();

        // Act
        var getCorrespondenceDetailsResponse = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.CorrespondenceIds.FirstOrDefault()}/details");
        Assert.True(getCorrespondenceDetailsResponse.IsSuccessStatusCode, await getCorrespondenceDetailsResponse.Content.ReadAsStringAsync());

        // Assert
        var response = await getCorrespondenceDetailsResponse.Content.ReadFromJsonAsync<CorrespondenceDetailsExt>(_responseSerializerOptions);
        Assert.DoesNotContain(response.StatusHistory, item => item.Status == CorrespondenceStatusExt.Fetched);
        Assert.Equal(response.Status, CorrespondenceStatusExt.Published);
    }
    [Fact]
    public async Task MarkActions_CorrespondenceNotExists_ReturnNotFound()
    {
        var readResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/00000000-0100-0000-0000-000000000000/markasread", null);
        Assert.Equal(HttpStatusCode.NotFound, readResponse.StatusCode);

        var confirmResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/00000000-0100-0000-0000-000000000000/confirm", null);
        Assert.Equal(HttpStatusCode.NotFound, confirmResponse.StatusCode);

        var archiveResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/00000000-0100-0000-0000-000000000000/archive", null);
        Assert.Equal(HttpStatusCode.NotFound, archiveResponse.StatusCode);
    }

    [Fact]
    public async Task ReceiverMarkActions_CorrespondenceNotPublished_Return404()
    {
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondences());
        var correspondenceResponse = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();
        Assert.NotNull(correspondenceResponse);
        var readResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/{correspondenceResponse?.CorrespondenceIds.FirstOrDefault()}/markasread", null);
        Assert.Equal(HttpStatusCode.NotFound, readResponse.StatusCode);

        var confirmResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/{correspondenceResponse?.CorrespondenceIds.FirstOrDefault()}/confirm", null);
        Assert.Equal(HttpStatusCode.NotFound, confirmResponse.StatusCode);
    }

    [Fact]
    public async Task ReceiverMarkActions_CorrespondencePublished_ReturnOk()
    {
        var uploadedAttachment = await InitializeAttachment();
        Assert.NotNull(uploadedAttachment);
        var payload = InitializeCorrespondenceFactory.BasicCorrespondences();
        payload.ExistingAttachments = new List<Guid> { uploadedAttachment.AttachmentId };
        payload.Correspondence.Content.Attachments = new List<InitializeCorrespondenceAttachmentExt>();
        payload.Correspondence.VisibleFrom = DateTime.UtcNow.AddMinutes(-1);
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
        var response = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
        Assert.NotNull(response);

        var overview = await _senderClient.GetFromJsonAsync<CorrespondenceOverviewExt>($"correspondence/api/v1/correspondence/{response.CorrespondenceIds.FirstOrDefault()}", _responseSerializerOptions);
        Assert.True(overview?.Status == CorrespondenceStatusExt.Published);
        Assert.NotNull(payload);
        var readResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/{response.CorrespondenceIds.FirstOrDefault()}/markasread", null);
        readResponse.EnsureSuccessStatusCode();

        var confirmResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/{response.CorrespondenceIds.FirstOrDefault()}/confirm", null);
        confirmResponse.EnsureSuccessStatusCode();

        var markAsUnreadResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/{response.CorrespondenceIds.FirstOrDefault()}/markasunread", null);
        markAsUnreadResponse.EnsureSuccessStatusCode();
        overview = await _senderClient.GetFromJsonAsync<CorrespondenceOverviewExt>($"correspondence/api/v1/correspondence/{response.CorrespondenceIds.FirstOrDefault()}", _responseSerializerOptions);
        Assert.True(overview?.MarkedUnread == true);
    }

    [Fact]
    public async Task Correspondence_with_dataLocationUrl_Reuses_Attachment()
    {
        var uploadedAttachment = await InitializeAttachment();
        var payload = InitializeCorrespondenceFactory.BasicCorrespondences();
        payload.ExistingAttachments = new List<Guid> { uploadedAttachment.AttachmentId };
        payload.Correspondence.Content.Attachments = new List<InitializeCorrespondenceAttachmentExt>();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
        var response = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
        Assert.Equal(uploadedAttachment.AttachmentId.ToString(), response?.AttachmentIds?.FirstOrDefault().ToString());
    }
    [Fact]
    public async Task DownloadCorrespondenceAttachment_AsRecipient_Succeeds()
    {
        // Arrange
        var attachment = InitializeAttachmentFactory.BasicAttachment();
        var initializeResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);

        var attachmentId = await initializeResponse.Content.ReadAsStringAsync();
        var uploadedAttachment = await (await UploadAttachment(attachmentId, new ByteArrayContent([1, 2, 3, 4]))).Content.ReadFromJsonAsync<AttachmentOverviewExt>(_responseSerializerOptions);

        var payload = InitializeCorrespondenceFactory.BasicCorrespondences();
        payload.ExistingAttachments = new List<Guid> { uploadedAttachment.AttachmentId };
        payload.Correspondence.Content!.Attachments = new List<InitializeCorrespondenceAttachmentExt>();

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
        var response = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();
        var downloadResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{response?.CorrespondenceIds.FirstOrDefault()}/attachment/{attachmentId}/download");
        var data = downloadResponse.Content.ReadAsByteArrayAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        Assert.NotNull(data);
    }
    [Fact]
    public async Task DownloadCorrespondenceAttachment_WhenNotARecipient_Returns404()
    {
        // Arrange
        var attachment = InitializeAttachmentFactory.BasicAttachment();
        var initializeResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);

        var attachmentId = await initializeResponse.Content.ReadAsStringAsync();
        var originalAttachmentData = new byte[] { 1, 2, 3, 4 };
        var content = new ByteArrayContent(originalAttachmentData);
        var uploadedAttachment = await (await UploadAttachment(attachmentId, content)).Content.ReadFromJsonAsync<AttachmentOverviewExt>(_responseSerializerOptions);

        var payload = InitializeCorrespondenceFactory.BasicCorrespondences();
        payload.ExistingAttachments = new List<Guid> { uploadedAttachment.AttachmentId };
        payload.Correspondence.Content!.Attachments = new List<InitializeCorrespondenceAttachmentExt>();
        payload.Recipients = ["0192:999999999"]; // Change recipient to invalid org

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
        var response = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();
        var downloadResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{response?.CorrespondenceIds.FirstOrDefault()}/attachment/{attachmentId}/download");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, downloadResponse.StatusCode);
    }
    [Fact]
    public async Task DownloadCorrespondenceAttachment_WhenCorrespondenceUnavailable_Returns404() // TODO: Fix initializeCorrespondence should check attachment is uploaded before 
    {
        // Arrange
        var attachment = InitializeAttachmentFactory.BasicAttachment();
        var initializeResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
        initializeResponse.EnsureSuccessStatusCode();

        var attachmentId = await initializeResponse.Content.ReadAsStringAsync();
        var originalAttachmentData = new byte[] { 1, 2, 3, 4 };
        var content = new ByteArrayContent(originalAttachmentData);
        await UploadAttachment(attachmentId, content);

        var payload = InitializeCorrespondenceFactory.BasicCorrespondences();
        payload.ExistingAttachments = new List<Guid> { Guid.Parse(attachmentId) };
        payload.Correspondence.Content!.Attachments = new List<InitializeCorrespondenceAttachmentExt>();
        payload.Correspondence.VisibleFrom = DateTimeOffset.UtcNow.AddDays(1); // Set visibleFrom in the future so that it is not published

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
        var response = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();
        var downloadResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{response?.CorrespondenceIds.FirstOrDefault()}/attachment/{attachmentId}/download");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, downloadResponse.StatusCode);
    }
    [Fact]
    public async Task DownloadCorrespondenceAttachment_WhenCorrespondenceHasNoAttachment_Returns404()
    {
        // Arrange
        var attachment = InitializeAttachmentFactory.BasicAttachment();
        var initializeResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
        initializeResponse.EnsureSuccessStatusCode();
        var attachmentId = await initializeResponse.Content.ReadAsStringAsync();
        await UploadAttachment(attachmentId);

        var payload = InitializeCorrespondenceFactory.BasicCorrespondenceWithoutAttachments();

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
        var response = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();
        var downloadResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{response?.CorrespondenceIds.FirstOrDefault()}/attachment/{attachmentId}/download");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, downloadResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_Initialized_Correspondence_AsSender_Gives_OK()
    {
        // Arrange
        var payload = InitializeCorrespondenceFactory.BasicCorrespondences();
        payload.Recipients = new List<string> { "0192:123456789" };
        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        var correspondenceResponse = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();
        Assert.NotNull(correspondenceResponse);
        var response = await _senderClient.DeleteAsync($"correspondence/api/v1/correspondence/{correspondenceResponse.CorrespondenceIds.FirstOrDefault()}/purge");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var overview = await _senderClient.GetFromJsonAsync<CorrespondenceOverviewExt>($"correspondence/api/v1/correspondence/{correspondenceResponse.CorrespondenceIds.FirstOrDefault()}", _responseSerializerOptions);
        Assert.Equal(overview?.Status, CorrespondenceStatusExt.PurgedByAltinn);
    }
    [Fact]
    public async Task Delete_Initialized_Correspondences_As_Receiver_Fails()
    {
        // Arrange
        var payload = InitializeCorrespondenceFactory.BasicCorrespondences();

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        var correspondenceResponse = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();
        Assert.NotNull(correspondenceResponse);
        var response = await _recipientClient.DeleteAsync($"correspondence/api/v1/correspondence/{correspondenceResponse.CorrespondenceIds.FirstOrDefault()}/purge");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    [Fact]
    public async Task Delete_Published_Correspondence_AsRecipient_Gives_OK()
    {
        // Arrange
        var payload = InitializeCorrespondenceFactory.BasicCorrespondenceWithoutAttachments();

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        var correspondenceResponse = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();
        Assert.NotNull(correspondenceResponse);
        var response = await _recipientClient.DeleteAsync($"correspondence/api/v1/correspondence/{correspondenceResponse.CorrespondenceIds.FirstOrDefault()}/purge");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var overview = await _senderClient.GetFromJsonAsync<CorrespondenceOverviewExt>($"correspondence/api/v1/correspondence/{correspondenceResponse.CorrespondenceIds.FirstOrDefault()}", _responseSerializerOptions);
        Assert.Equal(overview?.Status, CorrespondenceStatusExt.PurgedByRecipient);
    }

    [Fact]
    public async Task Delete_Published_Correspondences_As_Sender_Fails()
    {
        // Arrange
        var payload = InitializeCorrespondenceFactory.BasicCorrespondenceWithoutAttachments();

        // Act
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        var correspondenceResponse = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();
        Assert.NotNull(correspondenceResponse);
        var response = await _senderClient.DeleteAsync($"correspondence/api/v1/correspondence/{correspondenceResponse.CorrespondenceIds.FirstOrDefault()}/purge");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    [Fact]
    public async Task Delete_Correspondence_Also_deletes_attachment()
    {
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondences());
        var correspondenceResponse = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();
        Assert.NotNull(correspondenceResponse);
        foreach (var correspondenceId in correspondenceResponse.CorrespondenceIds)
        {
            var response = await _senderClient.DeleteAsync($"correspondence/api/v1/correspondence/{correspondenceId}/purge");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var overview = await _senderClient.GetFromJsonAsync<CorrespondenceOverviewExt>($"correspondence/api/v1/correspondence/{correspondenceId}", _responseSerializerOptions);
            Assert.Equal(overview?.Status, CorrespondenceStatusExt.PurgedByAltinn);
        }
        var attachment = await _senderClient.GetFromJsonAsync<AttachmentOverviewExt>($"correspondence/api/v1/attachment/{correspondenceResponse.AttachmentIds.FirstOrDefault()}", _responseSerializerOptions);
        Assert.Equal(attachment?.Status, AttachmentStatusExt.Purged);
    }

    [Fact]
    public async Task Delete_correspondence_dont_delete_attachment_with_multiple_correspondences()
    {
        var attachment = await InitializeAttachment();
        Assert.NotNull(attachment);
        var correspondence1 = InitializeCorrespondenceFactory.BasicCorrespondences();
        correspondence1.ExistingAttachments = new List<Guid> { attachment.AttachmentId };
        var correspondence2 = InitializeCorrespondenceFactory.BasicCorrespondences();
        correspondence2.ExistingAttachments = new List<Guid> { attachment.AttachmentId };

        var initializeCorrespondenceResponse1 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence1, _responseSerializerOptions);
        var response1 = await initializeCorrespondenceResponse1.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();
        initializeCorrespondenceResponse1.EnsureSuccessStatusCode();
        Assert.NotNull(response1);

        var initializeCorrespondenceResponse2 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence2, _responseSerializerOptions);
        var response2 = await initializeCorrespondenceResponse2.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();
        initializeCorrespondenceResponse2.EnsureSuccessStatusCode();
        Assert.NotNull(response2);

        var deleteResponse = await _senderClient.DeleteAsync($"correspondence/api/v1/correspondence/{response1.CorrespondenceIds.FirstOrDefault()}/purge");
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

    private async Task<HttpResponseMessage> UploadAttachment(string? attachmentId, ByteArrayContent? originalAttachmentData = null)
    {
        if (attachmentId == null)
        {
            Assert.Fail("AttachmentId is null");
        }
        var data = originalAttachmentData ?? new ByteArrayContent(new byte[] { 1, 2, 3, 4 });

        var uploadResponse = await _senderClient.PostAsync($"correspondence/api/v1/attachment/{attachmentId}/upload", data);
        return uploadResponse;
    }
    private async Task<AttachmentOverviewExt?> InitializeAttachment()
    {
        var attachment = InitializeAttachmentFactory.BasicAttachment();
        var initializeResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
        initializeResponse.EnsureSuccessStatusCode();
        var attachmentId = await initializeResponse.Content.ReadAsStringAsync();
        var overview = await (await UploadAttachment(attachmentId)).Content.ReadFromJsonAsync<AttachmentOverviewExt>(_responseSerializerOptions);
        return overview;
    }
    private MultipartFormDataContent CorrespondenceToFormData(BaseCorrespondenceExt correspondence)
    {
        var formData = new MultipartFormDataContent(){
            { new StringContent(correspondence.ResourceId), "correspondence.resourceId" },
            { new StringContent(correspondence.Sender), "correspondence.sender" },
            { new StringContent(correspondence.SendersReference), "correspondence.sendersReference" },
            { new StringContent(correspondence.VisibleFrom.ToString()), "correspondence.visibleFrom" },
            { new StringContent(correspondence.DueDateTime.ToString()), "correspondence.dueDateTime" },
            { new StringContent(correspondence.AllowSystemDeleteAfter.ToString()), "correspondence.AllowSystemDeleteAfter" },
            { new StringContent(correspondence.Content.MessageTitle), "correspondence.content.MessageTitle" },
            { new StringContent(correspondence.Content.MessageSummary), "correspondence.content.MessageSummary" },
            { new StringContent(correspondence.Content.MessageBody), "correspondence.content.MessageBody" },
            { new StringContent(correspondence.Content.Language), "correspondence.content.Language" },
            { new StringContent((correspondence.IsReservable ?? false).ToString()), "correspondence.isReservable" },
            { new StringContent(correspondence.Notification.NotificationTemplate.ToString()), "correspondence.Notification.NotificationTemplate" },
            { new StringContent(correspondence.Notification.RequestedSendTime.ToString()), "correspondence.Notification.RequestedSendTime" }
        };

        if (correspondence.Notification.EmailBody != null) formData.Add(new StringContent(correspondence.Notification.EmailBody), "correspondence.Notification.EmailBody");
        if (correspondence.Notification.EmailSubject != null) formData.Add(new StringContent(correspondence.Notification.EmailSubject), "correspondence.Notification.EmailSubject");
        if (correspondence.Notification.ReminderEmailBody != null) formData.Add(new StringContent(correspondence.Notification.ReminderEmailBody), "correspondence.Notification.ReminderEmailBody");
        if (correspondence.Notification.ReminderEmailSubject != null) formData.Add(new StringContent(correspondence.Notification.ReminderEmailSubject), "correspondence.Notification.ReminderEmailSubject");
        if (correspondence.Notification.SmsBody != null) formData.Add(new StringContent(correspondence.Notification.SmsBody), "correspondence.Notification.SmsBody");
        if (correspondence.Notification.ReminderSmsBody != null) formData.Add(new StringContent(correspondence.Notification.ReminderSmsBody), "correspondence.Notification.ReminderSmsBody");

        correspondence.Content.Attachments.Select((attachment, index) => new[]
        {
            new { Key = $"correspondence.content.Attachments[{index}].DataLocationType", Value = attachment.DataLocationType.ToString() },
            new { Key = $"correspondence.content.Attachments[{index}].DataType", Value = attachment.DataType },
            new { Key = $"correspondence.content.Attachments[{index}].Name", Value = attachment.Name },
            new { Key = $"correspondence.content.Attachments[{index}].FileName", Value = attachment.FileName ?? "" },
            new { Key = $"correspondence.content.Attachments[{index}].RestrictionName", Value = attachment.RestrictionName },
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
