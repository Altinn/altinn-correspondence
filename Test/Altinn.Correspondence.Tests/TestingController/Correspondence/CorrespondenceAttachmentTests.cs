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
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;

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
        [InlineData(".gif")]
        [InlineData(".bmp")]
        [InlineData(".png")]
        [InlineData(".json")]
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
        public async Task UploadCorrespondence_WithAttachmentExpirationTimeBeforeMinimum_GivesBadRequest()
        {
            using var stream = File.OpenRead("./Data/Markdown.txt");
            var file = new FormFile(stream, 0, stream.Length, null, Path.GetFileName(stream.Name));
            using var fileStream = file.OpenReadStream();

            var attachmentMetaData = AttachmentHelper.GetAttachmentMetaData(file.FileName);
            attachmentMetaData.ExpirationTime = DateTimeOffset.UtcNow.AddDays(13);
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([$"{UrnConstants.OrganizationNumberAttribute}:986252932"])
                .WithAttachments([attachmentMetaData])
                .Build();
            var formData = CorrespondenceHelper.CorrespondenceToFormData(payload.Correspondence);
            formData.Add(new StringContent($"{UrnConstants.OrganizationNumberAttribute}:986252932"), "recipients[0]");
            formData.Add(new StringContent(attachmentMetaData.ExpirationTime!.Value.ToString("o")), "correspondence.content.attachments[0].expirationTime");
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
            attachmentMetaData.ExpirationTime = DateTimeOffset.UtcNow.AddDays(15);
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([$"{UrnConstants.OrganizationNumberAttribute}:986252932"])
                .WithAttachments([attachmentMetaData])
                .Build();
            var formData = CorrespondenceHelper.CorrespondenceToFormData(payload.Correspondence);
            formData.Add(new StringContent($"{UrnConstants.OrganizationNumberAttribute}:986252932"), "recipients[0]");
            formData.Add(new StringContent(attachmentMetaData.ExpirationTime!.Value.ToString("o")), "correspondence.content.attachments[0].expirationTime");
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
                    .Setup(x => x.CheckAttachmentAccessAsRecipient(It.IsAny<ClaimsPrincipal?>(), It.IsAny<CorrespondenceEntity>(), It.IsAny<CancellationToken>()))
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
                    .Setup(x => x.CheckAttachmentAccessAsRecipient(It.IsAny<ClaimsPrincipal?>(), It.IsAny<CorrespondenceEntity>(), It.IsAny<CancellationToken>()))
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
    }
}
