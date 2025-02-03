using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Correspondence.Base;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.Net.Http.Json;

namespace Altinn.Correspondence.Tests.TestingController.Correspondence
{
    [Collection(nameof(CustomWebApplicationTestsCollection))]
    public class CorrespondenceAttachmentTests : CorrespondenceTestBase
    {
        public CorrespondenceAttachmentTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        [Fact]
        public async Task UploadCorrespondence_Gives_Ok()
        {
            // Arrange
            using var stream = File.OpenRead("./Data/Markdown.txt");
            var file = new FormFile(stream, 0, stream.Length, null, Path.GetFileName(stream.Name));
            var attachmentData = AttachmentHelper.GetAttachmentMetaData(file.FileName);
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithAttachments([attachmentData])
                .Build();
            var formData = CorrespondenceToFormData(payload.Correspondence);
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
            formData = CorrespondenceToFormData(payload2.Correspondence);
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
        public async Task UploadCorrespondence_Gives_Ok_For_All_Supported_FileTypes(string fileType)
        {
            // Arrange
            using var stream = File.OpenRead("./Data/FiletypeTestFiles/test" + fileType);
            var file = new FormFile(stream, 0, stream.Length, null, Path.GetFileName(stream.Name));
            var attachmentData = AttachmentHelper.GetAttachmentMetaData(file.FileName);
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithAttachments([attachmentData])
                .Build();
            var formData = CorrespondenceToFormData(payload.Correspondence);
            formData.Add(new StringContent($"{UrnConstants.OrganizationNumberAttribute}:986252932"), "recipients[0]");
            using var fileStream = file.OpenReadStream();
            formData.Add(new StreamContent(fileStream), "attachments", file.FileName);

            // Act
            var uploadCorrespondenceResponse = await _senderClient.PostAsync("correspondence/api/v1/correspondence/upload", formData);
            // Assert
            Assert.True(uploadCorrespondenceResponse.IsSuccessStatusCode, await uploadCorrespondenceResponse.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task UploadCorrespondence_Gives_BadRequest_When_Uploading_UnSupported_FileType()
        {
            // Arrange
            using var stream = File.OpenRead("./Data/FiletypeTestFiles/test.text");
            var file = new FormFile(stream, 0, stream.Length, null, Path.GetFileName(stream.Name));
            var attachmentData = AttachmentHelper.GetAttachmentMetaData(file.FileName);
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithAttachments([attachmentData])
                .Build();
            var formData = CorrespondenceToFormData(payload.Correspondence);
            formData.Add(new StringContent($"{UrnConstants.OrganizationNumberAttribute}:986252932"), "recipients[0]");
            using var fileStream = file.OpenReadStream();
            formData.Add(new StreamContent(fileStream), "attachments", file.FileName);

            // Act
            var uploadCorrespondenceResponse = await _senderClient.PostAsync("correspondence/api/v1/correspondence/upload", formData);
            // Assert
            Assert.True(uploadCorrespondenceResponse.StatusCode == HttpStatusCode.BadRequest, await uploadCorrespondenceResponse.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task UploadCorrespondenceWithoutAttachments_Gives_Ok()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .Build();
            var formData = CorrespondenceToFormData(payload.Correspondence);
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
        public async Task UploadCorrespondence_With_Multiple_Files()
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
            var formData = CorrespondenceToFormData(payload.Correspondence);
            formData.Add(new StringContent($"{UrnConstants.OrganizationNumberAttribute}:986252932"), "recipients[0]");
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

            var formData = CorrespondenceToFormData(payload.Correspondence);
            formData.Add(new StreamContent(fileStream), "attachments", file.FileName);
            formData.Add(new StreamContent(fileStream2), "attachments", file2.FileName);
            formData.Add(new StringContent($"{UrnConstants.OrganizationNumberAttribute}:986252932"), "recipients[0]");
            formData.Add(new StringContent($"{UrnConstants.OrganizationNumberAttribute}:991234649"), "recipients[1]");

            var uploadCorrespondenceResponse = await _senderClient.PostAsync("correspondence/api/v1/correspondence/upload", formData);
            Assert.True(uploadCorrespondenceResponse.IsSuccessStatusCode, await uploadCorrespondenceResponse.Content.ReadAsStringAsync());
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
}
