using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Attachment.Base;
using System.Net;

namespace Altinn.Correspondence.Tests.TestingController.Attachment
{
    [Collection(nameof(CustomWebApplicationTestsCollection))]
    public class AttachmentRetrievalTests : AttachmentTestBase
    {
        public AttachmentRetrievalTests(CustomWebApplicationFactory factory) : base(factory)
        {
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
        public async Task GetAttachmentOverview_As_Different_sender_ReturnsUnauthorized()
        {
            var attachmentId = await AttachmentHelper.GetInitializedAttachment(_senderClient, _responseSerializerOptions);
            var getAttachmentOverviewResponse = await _wrongSenderClient.GetAsync($"correspondence/api/v1/attachment/{attachmentId}");
            Assert.Equal(HttpStatusCode.Unauthorized, getAttachmentOverviewResponse.StatusCode);
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
        public async Task GetAttachmentDetails_As_Different_sender_ReturnsUnauthorized()
        {
            var attachmentId = await AttachmentHelper.GetInitializedAttachment(_senderClient, _responseSerializerOptions);
            var getAttachmentOverviewResponse = await _wrongSenderClient.GetAsync($"correspondence/api/v1/attachment/{attachmentId}/details");
            Assert.Equal(HttpStatusCode.Unauthorized, getAttachmentOverviewResponse.StatusCode);
        }
    }
}
