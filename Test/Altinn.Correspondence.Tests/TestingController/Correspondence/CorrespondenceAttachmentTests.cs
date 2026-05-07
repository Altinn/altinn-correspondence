using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Correspondence.Base;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Altinn.Correspondence.Application;
using Altinn.Correspondence.Application.Settings;

namespace Altinn.Correspondence.Tests.TestingController.Correspondence
{
    [Collection(nameof(CustomWebApplicationTestsCollection))]
    public class CorrespondenceAttachmentTests : CorrespondenceTestBase
    {
        public CorrespondenceAttachmentTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        [Fact]
        public async Task UploadCorrespondence_GivesOk()
        {
            // Arrange
            using var stream = File.OpenRead("./Data/Markdown.txt");
            var file = new FormFile(stream, 0, stream.Length, null, Path.GetFileName(stream.Name));
            var attachmentData = AttachmentHelper.GetAttachmentMetaData(file.FileName);
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithAttachments([attachmentData])
                .Build();
            var formData = CorrespondenceHelper.CorrespondenceToFormData(payload.Correspondence);
            formData.Add(new StringContent($"{UrnConstants.OrganizationNumberAttribute}:986252932"), "recipients[0]");
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
            formData = CorrespondenceHelper.CorrespondenceToFormData(payload2.Correspondence);
            formData.Add(new StreamContent(fileStream), "attachments", file.FileName);

            // Act
            var uploadCorrespondenceResponse2 = await _senderClient.PostAsync("correspondence/api/v1/correspondence/upload", formData);
            // Assert
            Assert.True(uploadCorrespondenceResponse.IsSuccessStatusCode, await uploadCorrespondenceResponse.Content.ReadAsStringAsync());
        }

        [Theory]
        [InlineData(".doc")]
        [InlineData(".xls")]
        [InlineData(".docx")]
        [InlineData(".xlsx")]
        [InlineData(".ppt")]
        [InlineData(".pps")]
        [InlineData(".zip")]
        [InlineData(".pdf")]
        [InlineData(".html")]
        [InlineData(".txt")]
        [InlineData(".xml")]
        [InlineData(".jpg")]
        [InlineData(".jpeg")]
        [InlineData(".gif")]
        [InlineData(".bmp")]
        [InlineData(".png")]
        [InlineData(".json")]
        [InlineData(".csv")]
        [InlineData(".dcm")]
        [InlineData(".dicom")]
        public async Task UploadCorrespondence_WithSupportedFileTypeAttachment_GivesOk(string filetype)
        {
            // Arrange
            using var memoryStream = new MemoryStream();
            memoryStream.Write("test"u8);
            var file = new FormFile(memoryStream, 0, memoryStream.Length, "file", "test" + filetype);
            var attachmentData = AttachmentHelper.GetAttachmentMetaData(file.FileName);
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithAttachments([attachmentData])
                .Build();
            var formData = CorrespondenceHelper.CorrespondenceToFormData(payload.Correspondence);
            formData.Add(new StringContent($"{UrnConstants.OrganizationNumberAttribute}:986252932"), "recipients[0]");
            using var fileStream = file.OpenReadStream();
            formData.Add(new StreamContent(fileStream), "attachments", file.FileName);

            // Act
            var uploadCorrespondenceResponse = await _senderClient.PostAsync("correspondence/api/v1/correspondence/upload", formData);

            // Assert
            Assert.True(uploadCorrespondenceResponse.IsSuccessStatusCode, await uploadCorrespondenceResponse.Content.ReadAsStringAsync());

            // Tear down
            memoryStream.Dispose();
        }

        [Fact]
        public async Task UploadCorrespondence_WithUnsupportedFileTypeAttachment_GivesBadRequest()
        {
            // Arrange
            using var memoryStream = new MemoryStream();
            memoryStream.Write("test"u8);
            var file = new FormFile(memoryStream, 0, memoryStream.Length, "file", "test.text");
            var attachmentData = AttachmentHelper.GetAttachmentMetaData(file.FileName);
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithAttachments([attachmentData])
                .Build();
            var formData = CorrespondenceHelper.CorrespondenceToFormData(payload.Correspondence);
            formData.Add(new StringContent($"{UrnConstants.OrganizationNumberAttribute}:986252932"), "recipients[0]");
            using var fileStream = file.OpenReadStream();
            formData.Add(new StreamContent(fileStream), "attachments", file.FileName);

            // Act
            var uploadCorrespondenceResponse = await _senderClient.PostAsync("correspondence/api/v1/correspondence/upload", formData);

            // Assert
            Assert.True(uploadCorrespondenceResponse.StatusCode == HttpStatusCode.BadRequest, await uploadCorrespondenceResponse.Content.ReadAsStringAsync());

            // Tear down
            memoryStream.Dispose();
        }

        [Fact]
        public async Task UploadCorrespondence_WithoutAttachments_GivesOk()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .Build();
            var formData = CorrespondenceHelper.CorrespondenceToFormData(payload.Correspondence);
            formData.Add(new StringContent($"{UrnConstants.OrganizationNumberAttribute}:986252932"), "recipients[0]");

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
        public async Task UploadCorrespondence_WithMultipleFiles_GivesOk()
        {
            using var stream = System.IO.File.OpenRead("./Data/Markdown.txt");
            var file = new FormFile(stream, 0, stream.Length, null, Path.GetFileName(stream.Name));
            using var fileStream = file.OpenReadStream();
            using var stream2 = System.IO.File.OpenRead("./Data/test.txt");
            var file2 = new FormFile(stream2, 0, stream2.Length, null, Path.GetFileName(stream2.Name));
            using var fileStream2 = file2.OpenReadStream();

            var attachmentMetaData = AttachmentHelper.GetAttachmentMetaData(file.FileName);
            var attachmentMetaData2 = AttachmentHelper.GetAttachmentMetaData(file2.FileName);
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([$"{UrnConstants.OrganizationNumberAttribute}:986252932"])
                .WithAttachments([attachmentMetaData, attachmentMetaData2])
                .Build();
            var formData = CorrespondenceHelper.CorrespondenceToFormData(payload.Correspondence);
            formData.Add(new StringContent($"{UrnConstants.OrganizationNumberAttribute}:986252932"), "recipients[0]");
            formData.Add(new StreamContent(fileStream), "attachments", file.FileName);
            formData.Add(new StreamContent(fileStream2), "attachments", file2.FileName);

            var uploadCorrespondenceResponse = await _senderClient.PostAsync("correspondence/api/v1/correspondence/upload", formData);
            Assert.Equal(HttpStatusCode.OK, uploadCorrespondenceResponse.StatusCode);
        }

        [Fact]
        public async Task UploadCorrespondence_WithInOrderAttachmentFileNames_GivesOk()
        {
            using var stream1 = new MemoryStream("first file content"u8.ToArray());
            using var stream2 = new MemoryStream("second file content"u8.ToArray());
            var file1 = new FormFile(stream1, 0, stream1.Length, "file1", "in-order-1.txt");
            var file2 = new FormFile(stream2, 0, stream2.Length, "file2", "in-order-2.txt");

            var attachment1 = AttachmentHelper.GetAttachmentMetaData(file1.FileName);
            var attachment2 = AttachmentHelper.GetAttachmentMetaData(file2.FileName);
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([$"{UrnConstants.OrganizationNumberAttribute}:986252932"])
                .WithAttachments([attachment1, attachment2])
                .Build();

            var formData = CorrespondenceHelper.CorrespondenceToFormData(payload.Correspondence);
            formData.Add(new StringContent($"{UrnConstants.OrganizationNumberAttribute}:986252932"), "recipients[0]");
            formData.Add(new StreamContent(file1.OpenReadStream()), "attachments", file1.FileName);
            formData.Add(new StreamContent(file2.OpenReadStream()), "attachments", file2.FileName);

            var uploadCorrespondenceResponse = await _senderClient.PostAsync("correspondence/api/v1/correspondence/upload", formData);
            Assert.Equal(HttpStatusCode.OK, uploadCorrespondenceResponse.StatusCode);
        }

        [Fact]
        public async Task UploadCorrespondence_WithOutOfOrderUniqueAttachmentFileNames_GivesOk()
        {
            using var stream1 = new MemoryStream("first file content"u8.ToArray());
            using var stream2 = new MemoryStream("second file content"u8.ToArray());
            var file1 = new FormFile(stream1, 0, stream1.Length, "file1", "out-of-order-1.txt");
            var file2 = new FormFile(stream2, 0, stream2.Length, "file2", "out-of-order-2.txt");

            var attachment1 = AttachmentHelper.GetAttachmentMetaData(file1.FileName);
            var attachment2 = AttachmentHelper.GetAttachmentMetaData(file2.FileName);
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([$"{UrnConstants.OrganizationNumberAttribute}:986252932"])
                .WithAttachments([attachment1, attachment2])
                .Build();

            var formData = CorrespondenceHelper.CorrespondenceToFormData(payload.Correspondence);
            formData.Add(new StringContent($"{UrnConstants.OrganizationNumberAttribute}:986252932"), "recipients[0]");
            formData.Add(new StreamContent(file2.OpenReadStream()), "attachments", file2.FileName);
            formData.Add(new StreamContent(file1.OpenReadStream()), "attachments", file1.FileName);

            var uploadCorrespondenceResponse = await _senderClient.PostAsync("correspondence/api/v1/correspondence/upload", formData);
            Assert.Equal(HttpStatusCode.OK, uploadCorrespondenceResponse.StatusCode);
        }

        [Fact]
        public async Task UploadCorrespondence_WithOutOfOrderAmbiguousDuplicateAttachmentFileNames_GivesBadRequest()
        {
            using var streamA1 = new MemoryStream("first file content"u8.ToArray());
            using var streamB = new MemoryStream("second file content"u8.ToArray());
            using var streamA2 = new MemoryStream("third file content"u8.ToArray());
            var fileA1 = new FormFile(streamA1, 0, streamA1.Length, "fileA1", "ambiguous-a.txt");
            var fileB = new FormFile(streamB, 0, streamB.Length, "fileB", "ambiguous-b.txt");
            var fileA2 = new FormFile(streamA2, 0, streamA2.Length, "fileA2", "ambiguous-a.txt");

            var attachmentA = AttachmentHelper.GetAttachmentMetaData(fileA1.FileName);
            var attachmentB = AttachmentHelper.GetAttachmentMetaData(fileB.FileName);
            var attachmentC = AttachmentHelper.GetAttachmentMetaData(fileA2.FileName);
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([$"{UrnConstants.OrganizationNumberAttribute}:986252932"])
                .WithAttachments([attachmentA, attachmentB, attachmentC])
                .Build();

            var formData = CorrespondenceHelper.CorrespondenceToFormData(payload.Correspondence);
            formData.Add(new StringContent($"{UrnConstants.OrganizationNumberAttribute}:986252932"), "recipients[0]");
            formData.Add(new StreamContent(fileB.OpenReadStream()), "attachments", fileB.FileName);
            formData.Add(new StreamContent(fileA1.OpenReadStream()), "attachments", fileA1.FileName);
            formData.Add(new StreamContent(fileA2.OpenReadStream()), "attachments", fileA2.FileName);

            var uploadCorrespondenceResponse = await _senderClient.PostAsync("correspondence/api/v1/correspondence/upload", formData);
            Assert.Equal(HttpStatusCode.BadRequest, uploadCorrespondenceResponse.StatusCode);
        }

        [Fact]
        public async Task UploadCorrespondence_WithAttachmentExpirationTimeBeforeMinimum_GivesBadRequest()
        {
            using var stream = File.OpenRead("./Data/Markdown.txt");
            var file = new FormFile(stream, 0, stream.Length, null, Path.GetFileName(stream.Name));
            using var fileStream = file.OpenReadStream();

            var attachmentMetaData = AttachmentHelper.GetAttachmentMetaData(file.FileName);
            attachmentMetaData.ExpirationInDays = 0;
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([$"{UrnConstants.OrganizationNumberAttribute}:986252932"])
                .WithAttachments([attachmentMetaData])
                .Build();
            var formData = CorrespondenceHelper.CorrespondenceToFormData(payload.Correspondence);
            formData.Add(new StringContent($"{UrnConstants.OrganizationNumberAttribute}:986252932"), "recipients[0]");
            formData.Add(new StringContent(attachmentMetaData.ExpirationInDays!.Value.ToString()), "correspondence.content.attachments[0].expirationInDays");
            formData.Add(new StreamContent(fileStream), "attachments", file.FileName);

            var uploadCorrespondenceResponse = await _senderClient.PostAsync("correspondence/api/v1/correspondence/upload", formData);
            Assert.Equal(HttpStatusCode.BadRequest, uploadCorrespondenceResponse.StatusCode);
        }

        [Fact]
        public async Task UploadCorrespondence_WithAttachmentExpirationTimeAfterMinimum_GivesOk()
        {
            using var stream = File.OpenRead("./Data/Markdown.txt");
            var file = new FormFile(stream, 0, stream.Length, null, Path.GetFileName(stream.Name));
            using var fileStream = file.OpenReadStream();

            var attachmentMetaData = AttachmentHelper.GetAttachmentMetaData(file.FileName);
            attachmentMetaData.ExpirationInDays = 2;
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([$"{UrnConstants.OrganizationNumberAttribute}:986252932"])
                .WithAttachments([attachmentMetaData])
                .Build();
            var formData = CorrespondenceHelper.CorrespondenceToFormData(payload.Correspondence);
            formData.Add(new StringContent($"{UrnConstants.OrganizationNumberAttribute}:986252932"), "recipients[0]");
            formData.Add(new StringContent(attachmentMetaData.ExpirationInDays!.Value.ToString()), "correspondence.content.attachments[0].expirationInDays");
            formData.Add(new StreamContent(fileStream), "attachments", file.FileName);

            var uploadCorrespondenceResponse = await _senderClient.PostAsync("correspondence/api/v1/correspondence/upload", formData);
            Assert.Equal(HttpStatusCode.OK, uploadCorrespondenceResponse.StatusCode);
        }

        [Fact]
        public async Task UploadCorrespondence_WithNoFiles_GivesBadrequest()
        {
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithAttachments([])
                .Build();
            var formData = CorrespondenceHelper.CorrespondenceToFormData(payload.Correspondence);
            var uploadCorrespondenceResponse = await _senderClient.PostAsync("correspondence/api/v1/correspondence/upload", formData);
            Assert.Equal(HttpStatusCode.BadRequest, uploadCorrespondenceResponse.StatusCode);
        }

        [Fact]
        public async Task UploadCorrespondences_WithMultipleFiles_GivesOk()
        {
            using var stream = File.OpenRead("./Data/Markdown.txt");
            var file = new FormFile(stream, 0, stream.Length, null, Path.GetFileName(stream.Name));
            using var fileStream = file.OpenReadStream();
            var attachmentMetaData = AttachmentHelper.GetAttachmentMetaData(file.FileName);

            using var stream2 = File.OpenRead("./Data/test.txt");
            var file2 = new FormFile(stream2, 0, stream2.Length, null, Path.GetFileName(stream2.Name));
            using var fileStream2 = file2.OpenReadStream();
            var attachmentMetaData2 = AttachmentHelper.GetAttachmentMetaData(file2.FileName);

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([$"{UrnConstants.OrganizationNumberAttribute}:986252932"])
                .WithAttachments([attachmentMetaData, attachmentMetaData2])
                .Build();

            var formData = CorrespondenceHelper.CorrespondenceToFormData(payload.Correspondence);
            formData.Add(new StreamContent(fileStream), "attachments", file.FileName);
            formData.Add(new StreamContent(fileStream2), "attachments", file2.FileName);
            formData.Add(new StringContent($"{UrnConstants.OrganizationNumberAttribute}:986252932"), "recipients[0]");
            formData.Add(new StringContent($"{UrnConstants.OrganizationNumberAttribute}:991234649"), "recipients[1]");

            var uploadCorrespondenceResponse = await _senderClient.PostAsync("correspondence/api/v1/correspondence/upload", formData);
            Assert.True(uploadCorrespondenceResponse.IsSuccessStatusCode, await uploadCorrespondenceResponse.Content.ReadAsStringAsync());
        }


        [Fact]
        public async Task Correspondence_WithDataLocationUrl_ReusesAttachment()
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
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _responseSerializerOptions, payload);
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, correspondence.CorrespondenceId, CorrespondenceStatusExt.Published);
            var downloadResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence.CorrespondenceId}/attachment/{attachmentId}/download");
            var data = downloadResponse.Content.ReadAsByteArrayAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
            Assert.NotNull(data);
        }

        [Fact]
        public async Task DownloadCorrespondenceAttachment_WhenAttachmentExpired_Fails()
        {
            // Arrange
            var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithExistingAttachments([attachmentId])
                .Build();

            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _responseSerializerOptions, payload);
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, correspondence.CorrespondenceId, CorrespondenceStatusExt.Published);

            using (var scope = _factory.Services.CreateScope())
            {
                var attachmentStatusRepository = scope.ServiceProvider.GetRequiredService<IAttachmentStatusRepository>();
                await attachmentStatusRepository.AddAttachmentStatus(new AttachmentStatusEntity
                {
                    AttachmentId = attachmentId,
                    Status = Altinn.Correspondence.Core.Models.Enums.AttachmentStatus.Expired,
                    StatusText = "The attachment has expired",
                    StatusChanged = DateTimeOffset.UtcNow,
                    PartyUuid = Guid.NewGuid()
                }, CancellationToken.None);
            }

            // Act
            var downloadResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence.CorrespondenceId}/attachment/{attachmentId}/download");

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, downloadResponse.StatusCode);
            var data = await downloadResponse.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal(AttachmentErrors.CannotDownloadExpiredAttachment.Message, data?.Detail);
        }

        [Fact]
        public async Task DownloadCorrespondenceAttachment_WhenNotARecipient_Returns401()
        {
            // Arrange
            var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithExistingAttachments([attachmentId])
                .WithRecipients([$"{UrnConstants.OrganizationNumberAttribute}:999999999"]) // Change recipient to invalid org
                .Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            var response = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            var downloadResponse = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{response?.Correspondences.FirstOrDefault().CorrespondenceId}/attachment/{attachmentId}/download");

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, downloadResponse.StatusCode);
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
        public async Task DownloadCorrespondenceAttachment_AddsAttachmentsDownloadedStatus()
        {
            // Arrange
            var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithExistingAttachments([attachmentId])
                .Build();

            // Act
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _responseSerializerOptions, payload);
            var correspondenceId = correspondence.CorrespondenceId;
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, correspondenceId, CorrespondenceStatusExt.Published);

            // Download the attachment
            var downloadResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/attachment/{attachmentId}/download");
            Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);

            // Get correspondence details to check status history
            var detailsResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/details");
            var details = await detailsResponse.Content.ReadFromJsonAsync<CorrespondenceDetailsExt>(_responseSerializerOptions);

            // Assert
            Assert.NotNull(details);
            Assert.Contains(details.StatusHistory, s =>
                s.Status == CorrespondenceStatusExt.AttachmentsDownloaded &&
                s.StatusText.Contains(attachmentId.ToString()));
        }

        [Fact]
        public async Task DownloadCorrespondenceAttachment_StatusHistoryShowsCorrectOrder()
        {
            // Arrange
            var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithExistingAttachments([attachmentId])
                .Build();

            // Act
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _responseSerializerOptions, payload);
            var correspondenceId = correspondence.CorrespondenceId;
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, correspondenceId, CorrespondenceStatusExt.Published);

            // Download the attachment
            var downloadResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/attachment/{attachmentId}/download");
            Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);

            // Get correspondence details to check status history
            var detailsResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/details");
            var details = await detailsResponse.Content.ReadFromJsonAsync<CorrespondenceDetailsExt>(_responseSerializerOptions);

            // Assert
            Assert.NotNull(details);
            var statusHistory = details.StatusHistory.ToList();
            var publishedStatus = statusHistory.FirstOrDefault(s => s.Status == CorrespondenceStatusExt.Published);
            var downloadedStatus = statusHistory.FirstOrDefault(s => s.Status == CorrespondenceStatusExt.AttachmentsDownloaded);

            Assert.NotNull(publishedStatus);
            Assert.NotNull(downloadedStatus);
            Assert.True(downloadedStatus.StatusChanged > publishedStatus.StatusChanged);
        }

        [Fact]
        public async Task DownloadCorrespondenceAttachment_WithAccessToAttachment_Succeeds()
        {
            using var customFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var mockAltinnAuthorization = new Mock<IAltinnAuthorizationService>();
                mockAltinnAuthorization
                    .Setup(x => x.CheckAttachmentAccessAsRecipient(It.IsAny<ClaimsPrincipal?>(), It.IsAny<CorrespondenceEntity>(), It.IsAny<AttachmentEntity>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                var existing = services.FirstOrDefault(d => d.ServiceType == typeof(IAltinnAuthorizationService));
                if (existing != null) services.Remove(existing);
                services.AddScoped(_ => mockAltinnAuthorization.Object);
            });

            var client = customFactory.CreateClientWithAddedClaims(
                ("notRecipient", "true"),
                ("scope", AuthorizationConstants.RecipientScope));

            // Arrange
            var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithExistingAttachments([attachmentId])
                .WithResourceId("2")
                .Build();

            // Act
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _responseSerializerOptions, payload);
            var correspondenceId = correspondence.CorrespondenceId;
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, correspondenceId, CorrespondenceStatusExt.Published);

            // Assert
            var downloadResponse = await client.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/attachment/{attachmentId}/download");
            Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        }

        [Fact]
        public async Task DownloadCorrespondenceAttachment_WithoutAccessToAttachment_ReturnsBadRequest()
        {

            using var customFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var mockAltinnAuthorization = new Mock<IAltinnAuthorizationService>();
                mockAltinnAuthorization
                    .Setup(x => x.CheckAttachmentAccessAsRecipient(It.IsAny<ClaimsPrincipal?>(), It.IsAny<CorrespondenceEntity>(), It.IsAny<AttachmentEntity>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false);

                var existing = services.FirstOrDefault(d => d.ServiceType == typeof(IAltinnAuthorizationService));
                if (existing != null) services.Remove(existing);
                services.AddScoped(_ => mockAltinnAuthorization.Object);
            });

            var client = customFactory.CreateClientWithAddedClaims(
                ("notSender", "true"),
                ("scope", AuthorizationConstants.RecipientScope));

            // Arrange
            var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions); //Default attachment builds with resourceId = "1"
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithExistingAttachments([attachmentId])
                .WithResourceId("2")
                .Build();

            // Act
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _responseSerializerOptions, payload);
            var correspondenceId = correspondence.CorrespondenceId;
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, correspondenceId, CorrespondenceStatusExt.Published);

            // Assert
            var downloadResponse = await client.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/attachment/{attachmentId}/download");
            Assert.Equal(HttpStatusCode.Unauthorized, downloadResponse.StatusCode);
        }

        [Fact]
        public async Task DownloadCorrespondenceAttachment_AccessToOneOutOfTwoAttachments_Succeeds_Then_ReturnsBadRequest()
        {
            var allowedId = Guid.Empty;
            using var customFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var mockAltinnAuthorization = new Mock<IAltinnAuthorizationService>();
                mockAltinnAuthorization
                    .Setup(x => x.CheckAttachmentAccessAsRecipient(
                        It.IsAny<ClaimsPrincipal?>(),
                        It.IsAny<CorrespondenceEntity>(),
                        It.IsAny<AttachmentEntity>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync((ClaimsPrincipal? user, CorrespondenceEntity correspondence, AttachmentEntity attachment, CancellationToken ct) =>
                    {
                        return attachment.Id == allowedId;
                    });

                var existing = services.FirstOrDefault(d => d.ServiceType == typeof(IAltinnAuthorizationService));
                if (existing != null) services.Remove(existing);
                services.AddScoped(_ => mockAltinnAuthorization.Object);
            });

            var recipientClient = customFactory.CreateClientWithAddedClaims(("notSender", "true"), ("scope", AuthorizationConstants.RecipientScope));

            // Arrange
            var attachmentAllowedId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
            allowedId = attachmentAllowedId;
            var attachmentDeniedId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithExistingAttachments([attachmentAllowedId, attachmentDeniedId])
                .Build();

            var initializeResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            initializeResponse.EnsureSuccessStatusCode();
            var initializeContent = await initializeResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            var correspondenceId = initializeContent!.Correspondences.First().CorrespondenceId;
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, correspondenceId, CorrespondenceStatusExt.Published);

            // Act & Assert
            var downloadAllowed = await recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/attachment/{attachmentAllowedId}/download");
            Assert.Equal(HttpStatusCode.OK, downloadAllowed.StatusCode);

            // Act & Assert 
            var downloadDenied = await recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/attachment/{attachmentDeniedId}/download");
            Assert.Equal(HttpStatusCode.Unauthorized, downloadDenied.StatusCode);
        }

        [Fact]
        public async Task DownloadAllCorrespondenceAttachments_AccessToAllAttachments_Succeeds()
        {
            using var customFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var mockAltinnAuthorization = new Mock<IAltinnAuthorizationService>();
                mockAltinnAuthorization
                    .Setup(x => x.CheckAttachmentAccessAsRecipient(It.IsAny<ClaimsPrincipal?>(), It.IsAny<CorrespondenceEntity>(), It.IsAny<AttachmentEntity>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                var existing = services.FirstOrDefault(d => d.ServiceType == typeof(IAltinnAuthorizationService));
                if (existing != null) services.Remove(existing);
                services.AddScoped(_ => mockAltinnAuthorization.Object);
            });

            var recipientClient = customFactory.CreateClientWithAddedClaims(("notSender", "true"), ("scope", AuthorizationConstants.RecipientScope));

            // Arrange
            var attachmentId1 = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
            var attachmentId2 = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithExistingAttachments([attachmentId1, attachmentId2])
                .Build();

            var initializeResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            initializeResponse.EnsureSuccessStatusCode();
            var initializeContent = await initializeResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            var correspondenceId = initializeContent!.Correspondences.First().CorrespondenceId;
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, correspondenceId, CorrespondenceStatusExt.Published);

            // Act
            var downloadResponse = await recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/attachments/downloadall");

            // Assert
            Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        }

        [Fact]
        public async Task DownloadAllCorrespondenceAttachments_AccessToOneOutOfTwoAttachments_ReturnsUnauthorized()
        {
            var allowedId = Guid.Empty;
            using var customFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var mockAltinnAuthorization = new Mock<IAltinnAuthorizationService>();
                mockAltinnAuthorization
                    .Setup(x => x.CheckAttachmentAccessAsRecipient(
                        It.IsAny<ClaimsPrincipal?>(),
                        It.IsAny<CorrespondenceEntity>(),
                        It.IsAny<AttachmentEntity>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync((ClaimsPrincipal? user, CorrespondenceEntity correspondence, AttachmentEntity attachment, CancellationToken ct) =>
                    {
                        return attachment.Id == allowedId;
                    });

                var existing = services.FirstOrDefault(d => d.ServiceType == typeof(IAltinnAuthorizationService));
                if (existing != null) services.Remove(existing);
                services.AddScoped(_ => mockAltinnAuthorization.Object);
            });

            var recipientClient = customFactory.CreateClientWithAddedClaims(("notSender", "true"), ("scope", AuthorizationConstants.RecipientScope));

            // Arrange
            var attachmentAllowedId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
            allowedId = attachmentAllowedId;
            var attachmentDeniedId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions, "differentResourceId");

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithExistingAttachments([attachmentAllowedId, attachmentDeniedId])
                .Build();

            var initializeResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            initializeResponse.EnsureSuccessStatusCode();
            var initializeContent = await initializeResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            var correspondenceId = initializeContent!.Correspondences.First().CorrespondenceId;
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, correspondenceId, CorrespondenceStatusExt.Published);

            // Act
            var downloadResponse = await recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/attachments/downloadall");
            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, downloadResponse.StatusCode);
        }

        [Fact]
        public async Task DownloadAllCorrespondenceAttachments_OneAttachmentExpired_ReturnsBadRequest()
        {
            // Arrange
            var attachmentId1 = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
            var attachmentId2 = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithExistingAttachments([attachmentId1, attachmentId2])
                .Build();

            var initializeResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            initializeResponse.EnsureSuccessStatusCode();
            var initializeContent = await initializeResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            var correspondenceId = initializeContent!.Correspondences.First().CorrespondenceId;
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, correspondenceId, CorrespondenceStatusExt.Published);

            using (var scope = _factory.Services.CreateScope())
            {
                var attachmentStatusRepository = scope.ServiceProvider.GetRequiredService<IAttachmentStatusRepository>();
                await attachmentStatusRepository.AddAttachmentStatus(new AttachmentStatusEntity
                {
                    AttachmentId = attachmentId1,
                    Status = Altinn.Correspondence.Core.Models.Enums.AttachmentStatus.Expired,
                    StatusText = "The attachment has expired",
                    StatusChanged = DateTimeOffset.UtcNow,
                    PartyUuid = Guid.NewGuid()
                }, CancellationToken.None);
            }

            // Act
            var downloadResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/attachments/downloadall");

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, downloadResponse.StatusCode);
            var data = await downloadResponse.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal(AttachmentErrors.CannotDownloadExpiredAttachment.Message, data?.Detail);
        }

        [Fact]
        public async Task DownloadAllCorrespondenceAttachments_WithOneOfEachAllowedFileType_ReturnsZipWithAllFiles()
        {
            // Arrange — upload one attachment per allowed file type
            var attachmentIds = new List<Guid>();
            foreach (var fileType in ApplicationConstants.AllowedFileTypes)
            {
                var attachment = new AttachmentBuilder()
                    .CreateAttachment()
                    .WithFileName($"test-file{fileType}")
                    .Build();
                var initResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
                Assert.Equal(HttpStatusCode.OK, initResponse.StatusCode);
                var id = await initResponse.Content.ReadFromJsonAsync<Guid>();
                var uploadResponse = await AttachmentHelper.UploadAttachment(id, _senderClient);
                Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
                await AttachmentHelper.WaitForAttachmentStatusUpdate(_senderClient, _responseSerializerOptions, id, AttachmentStatusExt.Published);
                attachmentIds.Add(id);
            }

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithExistingAttachments(attachmentIds)
                .Build();

            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _responseSerializerOptions, payload);
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, correspondence.CorrespondenceId, CorrespondenceStatusExt.Published);

            // Act
            var downloadResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence.CorrespondenceId}/attachments/downloadall");

            // Assert
            Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
            Assert.Equal("application/zip", downloadResponse.Content.Headers.ContentType?.MediaType);

            var zipBytes = await downloadResponse.Content.ReadAsByteArrayAsync();
            Assert.NotEmpty(zipBytes);

            using var zipStream = new System.IO.MemoryStream(zipBytes);
            using var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Read);
            Assert.Equal(ApplicationConstants.AllowedFileTypes.Count, archive.Entries.Count);
            foreach (var fileType in ApplicationConstants.AllowedFileTypes)
            {
                Assert.Contains(archive.Entries, e => e.Name.EndsWith(fileType, StringComparison.OrdinalIgnoreCase));
            }
        }

    }
}