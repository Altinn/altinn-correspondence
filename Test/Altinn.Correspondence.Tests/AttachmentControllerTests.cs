using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.Configuration;
using Altinn.Correspondence.Integrations.Altinn.Authorization;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Altinn.Correspondence.Tests;

public class AttachmentControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _senderClient;
    private readonly HttpClient _recipientClient;
    private readonly JsonSerializerOptions _responseSerializerOptions;
    private readonly string _userId = "0192:991825827";

    public AttachmentControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _senderClient = factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.SenderScope));
        _recipientClient = factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.RecipientScope));

        _responseSerializerOptions = new JsonSerializerOptions(new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        });
        _responseSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    }

    [Fact]
    public async Task InitializeAttachment()
    {
        var attachmentId = await AttachmentHelper.GetInitializedAttachment(_senderClient, _responseSerializerOptions);
        Assert.NotNull(attachmentId);
    }
    [Fact]
    public async Task InitializeAttachment_AsRecipient_ReturnsForbidden()
    {
        var attachment = new AttachmentBuilder().CreateAttachment().Build();
        var initializeAttachmentResponse = await _recipientClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
        Assert.Equal(HttpStatusCode.Forbidden, initializeAttachmentResponse.StatusCode);
    }

    [Fact]
    public async Task InitializeAttachment_WithWrongSender_ReturnsBadRequest()
    {
        var attachment = new AttachmentBuilder()
            .CreateAttachment()
            .WithSender("invalid-sender")
            .Build();
        var initializeAttachmentResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
        Assert.Equal(HttpStatusCode.BadRequest, initializeAttachmentResponse.StatusCode);

        var attachment2 = new AttachmentBuilder()
            .CreateAttachment()
            .WithSender("123456789")
            .Build();
        var initializeAttachmentResponse2 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment2);
        Assert.Equal(HttpStatusCode.BadRequest, initializeAttachmentResponse2.StatusCode);
    }

    [Fact]
    public async Task GetAttachmentOverview()
    {
        var attachmentId = await AttachmentHelper.GetInitializedAttachment(_senderClient, _responseSerializerOptions);
        var getAttachmentOverviewResponse = await _senderClient.GetAsync($"correspondence/api/v1/attachment/{attachmentId}");
        Assert.True(getAttachmentOverviewResponse.IsSuccessStatusCode, await getAttachmentOverviewResponse.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task GetAttachmentOverview_AsRecipient_ReturnsForbidden()
    {
        var attachmentId = await AttachmentHelper.GetInitializedAttachment(_senderClient, _responseSerializerOptions);
        var getAttachmentOverviewResponse = await _recipientClient.GetAsync($"correspondence/api/v1/attachment/{attachmentId}");
        Assert.Equal(HttpStatusCode.Forbidden, getAttachmentOverviewResponse.StatusCode);
    }

    [Fact]
    public async Task GetAttachmentDetails()
    {
        var attachmentId = await AttachmentHelper.GetInitializedAttachment(_senderClient, _responseSerializerOptions);
        var getAttachmentOverviewResponse = await _senderClient.GetAsync($"correspondence/api/v1/attachment/{attachmentId}/details");
        Assert.True(getAttachmentOverviewResponse.IsSuccessStatusCode, await getAttachmentOverviewResponse.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task GetAttachmentDetails_AsRecipient_ReturnsForbidden()
    {
        var attachmentId = await AttachmentHelper.GetInitializedAttachment(_senderClient, _responseSerializerOptions);
        var getAttachmentDetailsResponse = await _recipientClient.GetAsync($"correspondence/api/v1/attachment/{attachmentId}/details");
        Assert.Equal(HttpStatusCode.Forbidden, getAttachmentDetailsResponse.StatusCode);
    }
    [Fact]
    public async Task UploadAttachmentData_WhenAttachmentDoesNotExist_ReturnsNotFound()
    {
        var uploadResponse = await AttachmentHelper.UploadAttachment(Guid.Parse("00000000-0100-0000-0000-000000000000"), _senderClient);
        Assert.Equal(HttpStatusCode.NotFound, uploadResponse.StatusCode);
    }

    [Fact]
    public async Task UploadAttachmentData_WhenAttachmentExists_Succeeds()
    {
        var attachmentId = await AttachmentHelper.GetInitializedAttachment(_senderClient, _responseSerializerOptions);
        var uploadResponse = await AttachmentHelper.UploadAttachment(attachmentId, _senderClient);
        Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UploadAttachmentData_UploadsTwice_FailsSecondAttempt()
    {
        // First upload
        var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);

        // Second upload
        var secondUploadResponse = await AttachmentHelper.UploadAttachment(attachmentId, _senderClient);

        Assert.False(secondUploadResponse.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, secondUploadResponse.StatusCode);
    }

    [Fact]
    public async Task UploadAttachmentData_UploadFails_GetErrorMessage()
    {
        var attachmentId = await AttachmentHelper.GetInitializedAttachment(_senderClient, _responseSerializerOptions);
        var content = new StreamContent(new MemoryStream([])); // Empty content to simulate failure

        var uploadResponse = await _senderClient.PostAsync($"correspondence/api/v1/attachment/{attachmentId}/upload", content);

        Assert.False(uploadResponse.IsSuccessStatusCode);
        var errorMessage = await uploadResponse.Content.ReadAsStringAsync();
        var error = JsonSerializer.Deserialize<ProblemDetails>(errorMessage);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task UploadAttachmentData_Succeeds_DownloadedBytesAreSame()
    {
        var attachmentId = await AttachmentHelper.GetInitializedAttachment(_senderClient, _responseSerializerOptions);
        var originalAttachmentData = new byte[] { 1, 2, 3, 4 };
        var content = new ByteArrayContent(originalAttachmentData);
        var uploadResponse = await AttachmentHelper.UploadAttachment(attachmentId, _senderClient, content);
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithExistingAttachments([attachmentId])
            .Build();

        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
        Assert.Equal(HttpStatusCode.OK, initializeCorrespondenceResponse.StatusCode);

        // Download the attachment data
        var downloadResponse = await _senderClient.GetAsync($"correspondence/api/v1/attachment/{attachmentId}/download");
        downloadResponse.EnsureSuccessStatusCode();

        var downloadedAttachmentData = await downloadResponse.Content.ReadAsByteArrayAsync();

        // Assert that the uploaded and downloaded bytes are the same
        Assert.Equal(originalAttachmentData, downloadedAttachmentData);
    }
    [Fact]
    public async Task UploadAtttachmentData_ChecksumCorrect_Succeeds()
    {
        // Arrange
        var data = "This is the contents of the uploaded file";
        var byteData = Encoding.UTF8.GetBytes(data);
        var attachment = new AttachmentBuilder()
            .CreateAttachment()
            .WithChecksum(byteData)
            .Build();

        // Act
        var initializeResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
        initializeResponse.EnsureSuccessStatusCode();
        var attachmentId = await initializeResponse.Content.ReadFromJsonAsync<Guid>();
        var content = new ByteArrayContent(byteData);
        var uploadResponse = await AttachmentHelper.UploadAttachment(attachmentId, _senderClient, content);

        // Assert
        Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task UploadAttachment_MismatchChecksum_Fails()
    {
        // Arrange
        var data = "This is the contents of the uploaded file";
        var byteData = Encoding.UTF8.GetBytes(data);
        var checksum = AttachmentHelper.CalculateChecksum(byteData);
        var attachment = new AttachmentBuilder()
            .CreateAttachment()
            .WithChecksum(checksum)
            .Build();

        var initializeResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
        initializeResponse.EnsureSuccessStatusCode();
        var attachmentId = await initializeResponse.Content.ReadFromJsonAsync<Guid>();

        // Act
        var modifiedByteData = Encoding.UTF8.GetBytes("This is NOT the contents of the uploaded file");
        var modifiedContent = new ByteArrayContent(modifiedByteData);
        var uploadResponse = await AttachmentHelper.UploadAttachment(attachmentId, _senderClient, modifiedContent);

        // Assert
        Assert.False(uploadResponse.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, uploadResponse.StatusCode);
    }
    [Fact]
    public async Task UploadAttachment_NoChecksumSetWhenInitialized_ChecksumSetAfterUpload()
    {
        // Arrange
        var attachmentId = await AttachmentHelper.GetInitializedAttachment(_senderClient, _responseSerializerOptions);
        var prevOverview = await _senderClient.GetFromJsonAsync<AttachmentOverviewExt>($"correspondence/api/v1/attachment/{attachmentId}", _responseSerializerOptions);
        Assert.Empty(prevOverview.Checksum);

        var data = "This is the contents of the uploaded file";
        var byteData = Encoding.UTF8.GetBytes(data);
        var checksum = AttachmentHelper.CalculateChecksum(byteData);

        var content = new ByteArrayContent(byteData);

        // Act
        var uploadResponse = await AttachmentHelper.UploadAttachment(attachmentId, _senderClient, content);
        var attachmentOverview = await _senderClient.GetFromJsonAsync<AttachmentOverviewExt>($"correspondence/api/v1/attachment/{attachmentId}", _responseSerializerOptions);

        // Assert
        Assert.NotEmpty(attachmentOverview.Checksum);
        Assert.Equal(checksum, attachmentOverview.Checksum);
    }
    [Fact]
    public async Task UploadAttachment_ChecksumSetWhenInitialized_SameChecksumSetAfterUpload()
    {
        // Arrange
        var data = "This is the contents of the uploaded file";
        var byteData = Encoding.UTF8.GetBytes(data);
        var checksum = AttachmentHelper.CalculateChecksum(byteData);
        var content = new ByteArrayContent(byteData);
        var attachment = new AttachmentBuilder()
            .CreateAttachment()
            .WithChecksum(checksum)
            .Build();

        // Act
        var initializeResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
        initializeResponse.EnsureSuccessStatusCode();
        var attachmentId = await initializeResponse.Content.ReadFromJsonAsync<Guid>();
        var prevOverview = await _senderClient.GetFromJsonAsync<AttachmentOverviewExt>($"correspondence/api/v1/attachment/{attachmentId}", _responseSerializerOptions);
        Assert.NotEmpty(prevOverview.Checksum);

        var uploadResponse = await AttachmentHelper.UploadAttachment(attachmentId, _senderClient, content);
        var attachmentOverview = await _senderClient.GetFromJsonAsync<AttachmentOverviewExt>($"correspondence/api/v1/attachment/{attachmentId}", _responseSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, uploadResponse.StatusCode);
        Assert.NotEmpty(attachmentOverview.Checksum);
        Assert.Equal(prevOverview.Checksum, attachmentOverview.Checksum);
    }
    [Fact]
    public async Task UploadAtttachmentData_AsRecipient_ReturnsForbidden()
    {
        // Arrange
        var attachment = new AttachmentBuilder().CreateAttachment().Build();
        
        // Act
        var uploadResponse = await _recipientClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, uploadResponse.StatusCode);
    }
    [Fact]
    public async Task DownloadAttachment_AsSender_Succeeds()
    {
        // Arrange
        var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);

        // Act
        var downloadResponse = await _senderClient.GetAsync($"correspondence/api/v1/attachment/{attachmentId}/download");
        var data = downloadResponse.Content.ReadAsByteArrayAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        Assert.NotEmpty(data.Result);
    }
    [Fact]
    public async Task DownloadAttachment_AsRecipient_ReturnsForbidden()
    {
        // Arrange
        var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);

        // Act
        var downloadResponse = await _recipientClient.GetAsync($"correspondence/api/v1/attachment/{attachmentId}/download");
        var data = downloadResponse.Content.ReadAsByteArrayAsync();

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, downloadResponse.StatusCode);
        Assert.Empty(data.Result);
    }
    [Fact]
    public async Task DeleteAttachment_WhenAttachmentDoesNotExist_ReturnsNotFound()
    {
        var deleteResponse = await _senderClient.DeleteAsync("correspondence/api/v1/attachment/00000000-0100-0000-0000-000000000000");
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteAttachment_WhenAttachmentIsIntiliazied_Succeeds()
    {
        var attachmentId = await AttachmentHelper.GetInitializedAttachment(_senderClient, _responseSerializerOptions);

        var deleteResponse = await _senderClient.DeleteAsync($"correspondence/api/v1/attachment/{attachmentId}");
        Assert.True(deleteResponse.IsSuccessStatusCode, await deleteResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DeleteAttachment_Twice_Fails()
    {
        var attachmentId = await AttachmentHelper.GetInitializedAttachment(_senderClient, _responseSerializerOptions);

        var deleteResponse = await _senderClient.DeleteAsync($"correspondence/api/v1/attachment/{attachmentId}");
        Assert.True(deleteResponse.IsSuccessStatusCode, await deleteResponse.Content.ReadAsStringAsync());

        var deleteResponse2 = await _senderClient.DeleteAsync($"correspondence/api/v1/attachment/{attachmentId}");
        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse2.StatusCode);
    }

    [Fact]
    public async Task DeleteAttachment_WhenAttachedCorrespondenceIsPublished_ReturnsBadRequest()
    {
        var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithExistingAttachments([attachmentId])
            .Build();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        var correspondenceResponse = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);

        var overview = await _senderClient.GetFromJsonAsync<CorrespondenceOverviewExt>($"correspondence/api/v1/correspondence/{correspondenceResponse?.Correspondences.FirstOrDefault().CorrespondenceId}", _responseSerializerOptions);
        Assert.True(overview?.Status == CorrespondenceStatusExt.Published);

        var deleteResponse = await _senderClient.DeleteAsync($"correspondence/api/v1/attachment/{attachmentId}");
        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);
    }
    [Fact]
    public async Task DeleteAttachment_WhenAttachedCorrespondenceIsReadyForPublish_Fails()
    {
        var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithExistingAttachments([attachmentId])
            .WithRequestedPublishTime(DateTime.UtcNow.AddDays(1))
            .Build();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        var correspondenceResponse = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        var overview = await _senderClient.GetFromJsonAsync<CorrespondenceOverviewExt>($"correspondence/api/v1/correspondence/{correspondenceResponse?.Correspondences.FirstOrDefault().CorrespondenceId}", _responseSerializerOptions);
        Assert.True(overview?.Status == CorrespondenceStatusExt.ReadyForPublish);

        var deleteResponse = await _senderClient.DeleteAsync($"correspondence/api/v1/attachment/{correspondenceResponse?.AttachmentIds.FirstOrDefault()}");
        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);
    }
    [Fact]
    public async Task DeleteAttachment_AsRecipient_ReturnsForbidden()
    {
        // Arrange
        var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
        var deleteResponse = await _recipientClient.DeleteAsync($"correspondence/api/v1/attachment/{attachmentId}");
        // Assert failure before correspondence is created
        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);

        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithExistingAttachments([attachmentId])
            .Build();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        var correspondenceResponse = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);

        var overview = await _senderClient.GetFromJsonAsync<CorrespondenceOverviewExt>($"correspondence/api/v1/correspondence/{correspondenceResponse?.Correspondences.FirstOrDefault().CorrespondenceId}", _responseSerializerOptions);
        Assert.Equal(CorrespondenceStatusExt.Published, overview?.Status);

        // Act
        deleteResponse = await _recipientClient.DeleteAsync($"correspondence/api/v1/attachment/{attachmentId}");
        // Assert 
        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);
    }
}