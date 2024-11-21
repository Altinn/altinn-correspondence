using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Attachment.Base;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace Altinn.Correspondence.Tests.TestingController.Attachment
{
    public class AttachmentUploadTests : AttachmentTestBase
    {
        public AttachmentUploadTests(CustomWebApplicationFactory factory) : base(factory)
        {
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
        public async Task UploadAttachmentData_WrongSender_Fails()
        {
            var attachmentId = await AttachmentHelper.GetInitializedAttachment(_senderClient, _responseSerializerOptions);
            var uploadResponse = await AttachmentHelper.UploadAttachment(attachmentId, _wrongSenderClient);
            Assert.Equal(HttpStatusCode.BadRequest, uploadResponse.StatusCode);
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
            var data = await downloadResponse.Content.ReadAsByteArrayAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
            Assert.NotEmpty(data);
        }
    }
}
