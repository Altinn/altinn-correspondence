using Altinn.Correspondece.Tests.Factories;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Altinn.Correspondence.Tests;

public class AttachmentControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AttachmentControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClientInternal();
    }

    [Fact]
    public async Task InitializeAttachment()
    {
        var initializeAttachmentResponse = await _client.PostAsJsonAsync("correspondence/api/v1/attachment", InitializeAttachmentFactory.BasicAttachment());
        Assert.True(initializeAttachmentResponse.IsSuccessStatusCode, await initializeAttachmentResponse.Content.ReadAsStringAsync());
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
        var attachmentData = new byte[] { 1, 2, 3, 4 };
        var content = new ByteArrayContent(attachmentData);

        var uploadResponse = await _client.PostAsync("correspondence/api/v1/attachment/00000000-0100-0000-0000-000000000000/upload", content);

        Assert.Equal(HttpStatusCode.NotFound, uploadResponse.StatusCode);
    }

    [Fact]
    public async Task UploadAttachmentData_WhenAttachmentExists_Succeeds()
    {
        var attachment = InitializeAttachmentFactory.BasicAttachment();
        var initializeResponse = await _client.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
        initializeResponse.EnsureSuccessStatusCode();

        var attachmentId = await initializeResponse.Content.ReadAsStringAsync();
        var attachmentData = new byte[] { 1, 2, 3, 4 };
        var content = new ByteArrayContent(attachmentData);

        var uploadResponse = await _client.PostAsync($"correspondence/api/v1/attachment/{attachmentId}/upload", content);

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
        var firstUploadResponse = await _client.PostAsync($"correspondence/api/v1/attachment/{attachmentId}/upload", content);
        Assert.True(firstUploadResponse.IsSuccessStatusCode, await firstUploadResponse.Content.ReadAsStringAsync());

        // Second upload
        content = new ByteArrayContent(attachmentData);
        var secondUploadResponse = await _client.PostAsync($"correspondence/api/v1/attachment/{attachmentId}/upload", content);

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

        // Upload the attachment data
        var uploadResponse = await _client.PostAsync($"correspondence/api/v1/attachment/{attachmentId}/upload", content);
        uploadResponse.EnsureSuccessStatusCode();

        // Download the attachment data
        var downloadResponse = await _client.GetAsync($"correspondence/api/v1/attachment/{attachmentId}/download");
        downloadResponse.EnsureSuccessStatusCode();

        var downloadedAttachmentData = await downloadResponse.Content.ReadAsByteArrayAsync();

        // Assert that the uploaded and downloaded bytes are the same
        Assert.Equal(originalAttachmentData, downloadedAttachmentData);
    }
}