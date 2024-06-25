using Altinn.Correspondece.Tests.Factories;
using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Altinn.Correspondence.Tests;

public class AttachmentControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _responseSerializerOptions;

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
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondenceWithFileAttachment(uploadedAttachment.DataLocationUrl), _responseSerializerOptions);
        var response = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondenceResponseExt>();

        var overviewResponse = await _client.GetFromJsonAsync<CorrespondenceOverviewExt>($"correspondence/api/v1/correspondence/{response?.CorrespondenceId.ToString()}", _responseSerializerOptions);
        var correspondenceAttachmentId = overviewResponse.Content.Attachments.First().AttachmentId.ToString();
        // Download the attachment data
        var downloadResponse = await _client.GetAsync($"correspondence/api/v1/correspondence/attachment/{correspondenceAttachmentId}/download");
        downloadResponse.EnsureSuccessStatusCode();

        var downloadedAttachmentData = await downloadResponse.Content.ReadAsByteArrayAsync();

        // Assert that the uploaded and downloaded bytes are the same
        Assert.Equal(originalAttachmentData, downloadedAttachmentData);
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
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondence());
        var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondenceResponseExt>();
        await UploadAttachment(correspondence?.AttachmentIds.First().ToString());

        var overview = await _client.GetFromJsonAsync<CorrespondenceOverviewExt>($"correspondence/api/v1/correspondence/{correspondence?.CorrespondenceId}", _responseSerializerOptions);
        Assert.True(overview?.Status == CorrespondenceStatusExt.Published || overview?.Status == CorrespondenceStatusExt.ReadyForPublish);

        var deleteResponse = await _client.DeleteAsync($"correspondence/api/v1/attachment/{correspondence?.AttachmentIds.FirstOrDefault()}");
        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);
    }
    [Fact]
    public async Task DeleteAttachment_WhenAttachedCorrespondenceIsInitialized_Fails()
    {
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondenceWithFileAttachment());
        var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondenceResponseExt>();
        await UploadAttachment(correspondence?.AttachmentIds.First().ToString());
        var overview = await _client.GetFromJsonAsync<CorrespondenceOverviewExt>($"correspondence/api/v1/correspondence/{correspondence?.CorrespondenceId}", _responseSerializerOptions);
        Assert.True(overview?.Status == CorrespondenceStatusExt.Initialized);

        var deleteResponse = await _client.DeleteAsync($"correspondence/api/v1/attachment/{correspondence?.AttachmentIds.FirstOrDefault()}");
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