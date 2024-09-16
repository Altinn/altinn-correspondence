using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Altinn.Correspondence.Tests;

public class AttachmentControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _responseSerializerOptions;
    private readonly string _userId = "0192:991825827";

    public AttachmentControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClientInternal();
        _responseSerializerOptions = new JsonSerializerOptions(new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        });
        _responseSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    }

    [Fact]
    public async Task InitializeAttachment()
    {
        var initializeAttachmentResponse = await _client.PostAsJsonAsync("correspondence/api/v1/attachment", InitializeAttachmentFactory.BasicAttachment());
        Assert.True(initializeAttachmentResponse.IsSuccessStatusCode, await initializeAttachmentResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task InitializeAttachment_WithWrongSender_ReturnsBadRequest()
    {
        var attachment = InitializeAttachmentFactory.BasicAttachment();
        attachment.Sender = "invalid-sender";
        var initializeAttachmentResponse = await _client.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
        Assert.Equal(HttpStatusCode.BadRequest, initializeAttachmentResponse.StatusCode);

        attachment.Sender = "123456789";
        initializeAttachmentResponse = await _client.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
        Assert.Equal(HttpStatusCode.BadRequest, initializeAttachmentResponse.StatusCode);
    }

    [Fact]
    public async Task GetAttachmentOverview()
    {
        var initializeAttachmentResponse = await _client.PostAsJsonAsync("correspondence/api/v1/attachment", InitializeAttachmentFactory.BasicAttachment());
        var attachmentId = Guid.Parse(await initializeAttachmentResponse.Content.ReadAsStringAsync());
        var getAttachmentOverviewResponse = await _client.GetAsync($"correspondence/api/v1/attachment/{attachmentId}");
        Assert.True(getAttachmentOverviewResponse.IsSuccessStatusCode, await getAttachmentOverviewResponse.Content.ReadAsStringAsync());
    }


    [Fact]
    public async Task GetAttachmentDetails()
    {
        var initializeAttachmentResponse = await _client.PostAsJsonAsync("correspondence/api/v1/attachment", InitializeAttachmentFactory.BasicAttachment());
        var attachmentId = Guid.Parse(await initializeAttachmentResponse.Content.ReadAsStringAsync());
        var getAttachmentOverviewResponse = await _client.GetAsync($"correspondence/api/v1/attachment/{attachmentId}/details");
        Assert.True(getAttachmentOverviewResponse.IsSuccessStatusCode, await getAttachmentOverviewResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UploadAttachmentData_WhenAttachmentDoesNotExist_ReturnsNotFound()
    {
        var uploadResponse = await UploadAttachment("00000000-0100-0000-0000-000000000000");
        Assert.Equal(HttpStatusCode.NotFound, uploadResponse.StatusCode);
    }

    [Fact]
    public async Task UploadAttachmentData_WhenAttachmentExists_Succeeds()
    {
        var attachment = InitializeAttachmentFactory.BasicAttachment();
        var initializeResponse = await _client.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
        initializeResponse.EnsureSuccessStatusCode();

        var attachmentId = await initializeResponse.Content.ReadAsStringAsync();
        var uploadResponse = await UploadAttachment(attachmentId);

        Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UploadAttachmentData_UploadsTwice_FailsSecondAttempt()
    {
        var attachment = InitializeAttachmentFactory.BasicAttachment();
        var initializeResponse = await _client.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
        initializeResponse.EnsureSuccessStatusCode();

        var attachmentId = await initializeResponse.Content.ReadAsStringAsync();
        var attachmentData = new byte[] { 1, 2, 3, 4 };
        var content = new ByteArrayContent(attachmentData);
        // First upload
        var firstUploadResponse = await UploadAttachment(attachmentId, content);
        Assert.True(firstUploadResponse.IsSuccessStatusCode, await firstUploadResponse.Content.ReadAsStringAsync());

        // Second upload
        var secondUploadResponse = await UploadAttachment(attachmentId, content);

        Assert.False(secondUploadResponse.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, secondUploadResponse.StatusCode);
    }

    [Fact]
    public async Task UploadAttachmentData_UploadFails_GetErrorMessage()
    {
        var attachment = InitializeAttachmentFactory.BasicAttachment();
        var initializeResponse = await _client.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
        initializeResponse.EnsureSuccessStatusCode();

        var attachmentId = await initializeResponse.Content.ReadAsStringAsync();
        var content = new StreamContent(new MemoryStream([])); // Empty content to simulate failure

        var uploadResponse = await _client.PostAsync($"correspondence/api/v1/attachment/{attachmentId}/upload", content);

        Assert.False(uploadResponse.IsSuccessStatusCode);
        var errorMessage = await uploadResponse.Content.ReadAsStringAsync();
        var error = JsonSerializer.Deserialize<ProblemDetails>(errorMessage);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task UploadAttachmentData_Succeeds_DownloadedBytesAreSame()
    {
        var attachment = InitializeAttachmentFactory.BasicAttachment();
        var initializeResponse = await _client.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
        initializeResponse.EnsureSuccessStatusCode();

        var attachmentId = await initializeResponse.Content.ReadAsStringAsync();
        var originalAttachmentData = new byte[] { 1, 2, 3, 4 };
        var content = new ByteArrayContent(originalAttachmentData);
        var uploadedAttachment = await (await UploadAttachment(attachmentId, content)).Content.ReadFromJsonAsync<AttachmentOverviewExt>(_responseSerializerOptions);
        Assert.NotNull(uploadedAttachment);
        var payload = InitializeCorrespondenceFactory.BasicCorrespondences();
        payload.ExistingAttachments = new List<Guid> { uploadedAttachment.AttachmentId };
        payload.Correspondence.Content!.Attachments = new List<InitializeCorrespondenceAttachmentExt>();
        payload.Recipients = [_userId];
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
        var response = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();

        var overviewResponse = await _client.GetFromJsonAsync<CorrespondenceOverviewExt>($"correspondence/api/v1/correspondence/{response?.CorrespondenceIds.FirstOrDefault().ToString()}", _responseSerializerOptions);
        var correspondenceAttachmentId = overviewResponse.Content.Attachments.First().Id.ToString();
        // Download the attachment data
        var downloadResponse = await _client.GetAsync($"correspondence/api/v1/correspondence/{response?.CorrespondenceIds.FirstOrDefault()}/attachment/{correspondenceAttachmentId}/download");
        downloadResponse.EnsureSuccessStatusCode();

        var downloadedAttachmentData = await downloadResponse.Content.ReadAsByteArrayAsync();

        // Assert that the uploaded and downloaded bytes are the same
        Assert.Equal(originalAttachmentData, downloadedAttachmentData);
    }
    [Fact]
    public async Task UploadAtttachmentData_ChecksumCorrect_Succeeds()
    {
        // Arrange
        var attachment = InitializeAttachmentFactory.BasicAttachment();
        var data = "This is the contents of the uploaded file";
        var byteData = Encoding.UTF8.GetBytes(data);
        var checksum = Utils.CalculateChecksum(byteData);
        attachment.Checksum = checksum;

        // Act
        var initializeResponse = await _client.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
        initializeResponse.EnsureSuccessStatusCode();
        var attachmentId = await initializeResponse.Content.ReadAsStringAsync();
        var content = new ByteArrayContent(byteData);
        var uploadResponse = await UploadAttachment(attachmentId, content);

        // Assert
        Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task UploadAttachment_MismatchChecksum_Fails()
    {
        // Arrange
        var attachment = InitializeAttachmentFactory.BasicAttachment();
        var data = "This is the contents of the uploaded file";
        var byteData = Encoding.UTF8.GetBytes(data);
        var checksum = Utils.CalculateChecksum(byteData);
        attachment.Checksum = checksum;

        var modifiedByteData = Encoding.UTF8.GetBytes("This is NOT the contents of the uploaded file");
        var modifiedContent = new ByteArrayContent(modifiedByteData);

        // Act
        var initializeResponse = await _client.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
        initializeResponse.EnsureSuccessStatusCode();
        var attachmentId = await initializeResponse.Content.ReadAsStringAsync();
        var uploadResponse = await UploadAttachment(attachmentId, modifiedContent);

        // Assert
        Assert.False(uploadResponse.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, uploadResponse.StatusCode);
    }
    [Fact]
    public async Task UploadAttachment_NoChecksumSetWhenInitialized_ChecksumSetAfterUpload()
    {
        // Arrange
        var attachment = InitializeAttachmentFactory.BasicAttachment();
        var initializeResponse = await _client.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
        initializeResponse.EnsureSuccessStatusCode();
        var attachmentId = await initializeResponse.Content.ReadAsStringAsync();
        var prevOverview = await _client.GetFromJsonAsync<AttachmentOverviewExt>($"correspondence/api/v1/attachment/{attachmentId}", _responseSerializerOptions);

        var data = "This is the contents of the uploaded file";
        var byteData = Encoding.UTF8.GetBytes(data);
        var checksum = Utils.CalculateChecksum(byteData);

        var content = new ByteArrayContent(byteData);

        // Act
        await UploadAttachment(attachmentId, content);
        var attachmentOverview = await _client.GetFromJsonAsync<AttachmentOverviewExt>($"correspondence/api/v1/attachment/{attachmentId}", _responseSerializerOptions);

        // Assert
        Assert.Empty(prevOverview.Checksum);
        Assert.NotEmpty(attachmentOverview.Checksum);
        Assert.Equal(checksum, attachmentOverview.Checksum);
    }
    [Fact]
    public async Task UploadAttachment_ChecksumSetWhenInitialized_SameChecksumSetAfterUpload()
    {
        // Arrange
        var attachment = InitializeAttachmentFactory.BasicAttachment();
        var data = "This is the contents of the uploaded file";
        var byteData = Encoding.UTF8.GetBytes(data);
        var checksum = Utils.CalculateChecksum(byteData);
        attachment.Checksum = checksum;

        var content = new ByteArrayContent(byteData);

        // Act
        var initializeResponse = await _client.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
        initializeResponse.EnsureSuccessStatusCode();
        var attachmentId = await initializeResponse.Content.ReadAsStringAsync();
        var prevOverview = await _client.GetFromJsonAsync<AttachmentOverviewExt>($"correspondence/api/v1/attachment/{attachmentId}", _responseSerializerOptions);
        await UploadAttachment(attachmentId, content);
        var attachmentOverview = await _client.GetFromJsonAsync<AttachmentOverviewExt>($"correspondence/api/v1/attachment/{attachmentId}", _responseSerializerOptions);

        // Assert
        Assert.NotEmpty(prevOverview.Checksum);
        Assert.NotEmpty(attachmentOverview.Checksum);
        Assert.Equal(prevOverview.Checksum, attachmentOverview.Checksum);
    }
    [Fact]
    public async Task UploadAttachment_WhenFailedCorrespondence_Fails()
    {
        // Arrange
        var payload = InitializeCorrespondenceFactory.BasicCorrespondences();
        payload.Correspondence.Sender = _userId;
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
        var response = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();
        var correspondenceId = response.CorrespondenceIds.FirstOrDefault();
        var attachmentId = response.AttachmentIds.FirstOrDefault();

        // Act
        using (var scope = _factory.Services.CreateScope()) // Add failed status to correspondence
        {
            var correspondenceStatusRepository = scope.ServiceProvider.GetRequiredService<ICorrespondenceStatusRepository>();
            await correspondenceStatusRepository.AddCorrespondenceStatus(new Core.Models.CorrespondenceStatusEntity()
            {
                CorrespondenceId = correspondenceId,
                Status = Core.Models.Enums.CorrespondenceStatus.Failed,
                StatusChanged = DateTime.UtcNow
            }, default);
        }
        var getCorrespondenceOverviewResponse = await _client.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}");
        Assert.True(getCorrespondenceOverviewResponse.IsSuccessStatusCode, await getCorrespondenceOverviewResponse.Content.ReadAsStringAsync());
        var overviewResponse = await getCorrespondenceOverviewResponse.Content.ReadFromJsonAsync<CorrespondenceOverviewExt>(_responseSerializerOptions);
        Assert.Equal(overviewResponse.Status, CorrespondenceStatusExt.Failed);
        var uploadResponse = await UploadAttachment(attachmentId.ToString()); // Attempt upload

        // Assert
        Assert.Equal(uploadResponse.StatusCode, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadAttachment_WhenCorresponodenceFailedDuringUpload_ReturnsErrorAndDisposesAttachment()
    {
        // Arrange
        var uploadFailsClient = new UploadFailsWebApplicationFactory().CreateClientInternal(); // Setup client which contains time delay during upload and no mock for hangfire
        var payload = InitializeCorrespondenceFactory.BasicCorrespondences();
        payload.Recipients = [payload.Recipients[0]];
        payload.Correspondence.Sender = _userId;
        payload.Correspondence.VisibleFrom = DateTimeOffset.UtcNow.AddSeconds(2);
        var initializeCorrespondenceResponse = await (await uploadFailsClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions)).Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();
        var correspondenceId = initializeCorrespondenceResponse.CorrespondenceIds.FirstOrDefault();
        var attachmentId = initializeCorrespondenceResponse.AttachmentIds.FirstOrDefault();

        // Act
        var attachmentOverview = await (await _client.GetAsync($"correspondence/api/v1/attachment/{attachmentId}")).Content.ReadFromJsonAsync<AttachmentOverviewExt>(_responseSerializerOptions);
        Assert.Equal(AttachmentStatusExt.Initialized, attachmentOverview.Status); // Verify attachment is ok
        var overviewResponse = await (await _client.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}")).Content.ReadFromJsonAsync<CorrespondenceOverviewExt>(_responseSerializerOptions);
        Assert.Equal(CorrespondenceStatusExt.Initialized, overviewResponse.Status); // Verify correspondence is ok

        var uploadResponse = await uploadFailsClient.PostAsync($"correspondence/api/v1/attachment/{attachmentId}/upload", new ByteArrayContent([1, 2, 3, 4]));// Attempt upload

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, uploadResponse.StatusCode);
        overviewResponse = await (await _client.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}")).Content.ReadFromJsonAsync<CorrespondenceOverviewExt>(_responseSerializerOptions);
        Assert.Equal(CorrespondenceStatusExt.Failed, overviewResponse.Status);
        attachmentOverview = await (await _client.GetAsync($"correspondence/api/v1/attachment/{attachmentId}")).Content.ReadFromJsonAsync<AttachmentOverviewExt>(_responseSerializerOptions);
        Assert.Equal(AttachmentStatusExt.Purged, attachmentOverview.Status);
    }
    [Fact]
    public async Task DeleteAttachment_WhenAttachmentDoesNotExist_ReturnsNotFound()
    {
        var deleteResponse = await _client.DeleteAsync("correspondence/api/v1/attachment/00000000-0100-0000-0000-000000000000");
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteAttachment_WhenAttachmentIsIntiliazied_Succeeds()
    {
        var attachment = InitializeAttachmentFactory.BasicAttachment();
        var initializeResponse = await _client.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
        initializeResponse.EnsureSuccessStatusCode();

        var attachmentId = await initializeResponse.Content.ReadAsStringAsync();

        var deleteResponse = await _client.DeleteAsync($"correspondence/api/v1/attachment/{attachmentId}");
        Assert.True(deleteResponse.IsSuccessStatusCode, await deleteResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DeleteAttachment_Twice_Fails()
    {
        var attachment = InitializeAttachmentFactory.BasicAttachment();
        var initializeResponse = await _client.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
        initializeResponse.EnsureSuccessStatusCode();

        var attachmentId = await initializeResponse.Content.ReadAsStringAsync();

        var deleteResponse = await _client.DeleteAsync($"correspondence/api/v1/attachment/{attachmentId}");
        Assert.True(deleteResponse.IsSuccessStatusCode, await deleteResponse.Content.ReadAsStringAsync());

        var deleteResponse2 = await _client.DeleteAsync($"correspondence/api/v1/attachment/{attachmentId}");
        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse2.StatusCode);
    }

    [Fact]
    public async Task DeleteAttachment_WhenAttachedCorrespondenceIsPublished_ReturnsBadRequest()
    {
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondences());
        var correspondenceResponse = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();
        await UploadAttachment(correspondenceResponse?.AttachmentIds.First().ToString());

        var overview = await _client.GetFromJsonAsync<CorrespondenceOverviewExt>($"correspondence/api/v1/correspondence/{correspondenceResponse?.CorrespondenceIds.FirstOrDefault()}", _responseSerializerOptions);
        Assert.True(overview?.Status == CorrespondenceStatusExt.Published || overview?.Status == CorrespondenceStatusExt.ReadyForPublish);

        var deleteResponse = await _client.DeleteAsync($"correspondence/api/v1/attachment/{correspondenceResponse?.AttachmentIds.FirstOrDefault()}");
        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);
    }
    [Fact]
    public async Task DeleteAttachment_WhenAttachedCorrespondenceIsInitialized_Fails()
    {
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondenceWithFileAttachment());
        var correspondenceResponse = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();
        await UploadAttachment(correspondenceResponse?.AttachmentIds.First().ToString());
        var overview = await _client.GetFromJsonAsync<CorrespondenceOverviewExt>($"correspondence/api/v1/correspondence/{correspondenceResponse?.CorrespondenceIds.FirstOrDefault()}", _responseSerializerOptions);
        Assert.True(overview?.Status == CorrespondenceStatusExt.Initialized);

        var deleteResponse = await _client.DeleteAsync($"correspondence/api/v1/attachment/{correspondenceResponse?.AttachmentIds.FirstOrDefault()}");
        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);
    }

    private async Task<HttpResponseMessage> UploadAttachment(string? attachmentId, ByteArrayContent? originalAttachmentData = null)
    {
        if (attachmentId == null)
        {
            Assert.Fail("AttachmentId is null");
        }
        var data = originalAttachmentData ?? new ByteArrayContent(new byte[] { 1, 2, 3, 4 });

        var uploadResponse = await _client.PostAsync($"correspondence/api/v1/attachment/{attachmentId}/upload", data);
        return uploadResponse;
    }

}