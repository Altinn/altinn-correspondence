using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Attachment.Base;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace Altinn.Correspondence.Tests.TestingController.Attachment
{
    [Collection(nameof(CustomWebApplicationTestsCollection))]
    public class AttachmentDownloadTests : AttachmentTestBase
    {
        public AttachmentDownloadTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        [Fact]
        public async Task DownloadAttachment_AsRecipient_ReturnsForbidden()
        {
            // Arrange
            var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);

            // Act
            var downloadResponse = await _recipientClient.GetAsync($"correspondence/api/v1/attachment/{attachmentId}/download");
            var data = await downloadResponse.Content.ReadAsByteArrayAsync();

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, downloadResponse.StatusCode);
            Assert.Empty(data);
        }
        [Fact]
        public async Task DownloadAttachment_AsWrongSender_ReturnsBadRequest()
        {
            // Arrange
            var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);

            // Act
            var downloadResponse = await _wrongSenderClient.GetAsync($"correspondence/api/v1/attachment/{attachmentId}/download");
            var data = await downloadResponse.Content.ReadFromJsonAsync<ProblemDetails>();

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, downloadResponse.StatusCode);
            Assert.NotNull(data?.Title);
        }

        [Fact]
        public async Task DownloadAttachment_AsSenderAfterAttachedToPublishedCorrespondence_Fails()
        {
            // Arrange
            var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
            var correspondencePayload = new CorrespondenceBuilder().CreateCorrespondence()
                .WithExistingAttachments([attachmentId])
                .Build();

            // Act
            var downloadResponseBeforeAttached = await _senderClient.GetAsync($"correspondence/api/v1/attachment/{attachmentId}/download");
            Assert.True(downloadResponseBeforeAttached.IsSuccessStatusCode, await downloadResponseBeforeAttached.Content.ReadAsStringAsync());
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondencePayload, CancellationToken.None);
            Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _responseSerializerOptions, correspondencePayload);
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, correspondence.CorrespondenceId, CorrespondenceStatusExt.Published);
            var downloadResponseAfterAttached = await _senderClient.GetAsync($"correspondence/api/v1/attachment/{attachmentId}/download");

            // Assert
            Assert.True(downloadResponseAfterAttached.StatusCode == HttpStatusCode.BadRequest, await downloadResponseAfterAttached.Content.ReadAsStringAsync());
            var data = await downloadResponseAfterAttached.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal(data.Detail, AttachmentErrors.AttachedToAPublishedCorrespondence.Message);
        }

        [Fact]
        public async Task DownloadAttachment_AsSenderAfterPurged_Fails()
        {
            // Arrange
            var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);

            // Act
            var purgeResponse = await _senderClient.DeleteAsync($"correspondence/api/v1/attachment/{attachmentId}");
            Assert.True(purgeResponse.IsSuccessStatusCode, await purgeResponse.Content.ReadAsStringAsync());
            var downloadResponse = await _senderClient.GetAsync($"correspondence/api/v1/attachment/{attachmentId}/download");

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, downloadResponse.StatusCode);
            var data = await downloadResponse.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal(AttachmentErrors.CannotDownloadPurgedAttachment.Message, data?.Detail);
        }

        [Fact]
        public async Task DownloadAttachment_AsSenderAfterExpired_Fails()
        {
            // Arrange
            var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
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
            var downloadResponse = await _senderClient.GetAsync($"correspondence/api/v1/attachment/{attachmentId}/download");

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, downloadResponse.StatusCode);
            var data = await downloadResponse.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal(AttachmentErrors.CannotDownloadExpiredAttachment.Message, data?.Detail);
        }
    }
}
