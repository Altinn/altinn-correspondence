using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Tests.Factories;

namespace Altinn.Correspondence.Tests.Helpers
{
    internal static class AttachmentHelper
    {
        public static InitializeCorrespondenceAttachmentExt GetAttachmentMetaData(string fileName, AttachmentOverviewExt existingAttachmentData = null)
        {
            var attachmentData = new InitializeCorrespondenceAttachmentExt()
            {
                SendersReference = existingAttachmentData?.SendersReference ?? "1234",
                FileName = existingAttachmentData?.FileName ?? fileName,
                IsEncrypted = existingAttachmentData?.IsEncrypted ?? false,
                Checksum = existingAttachmentData?.Checksum,
            };
            return attachmentData;
        }
        public static async Task<Guid> GetInitializedAttachment(HttpClient client, JsonSerializerOptions responseSerializerOptions, string? sender = null)
        {
            var tempData = new AttachmentBuilder().CreateAttachment();
            if (sender != null)
            {
                tempData.WithSender(sender);
            }
            var attachment = tempData.Build();
            var initializeAttachmentResponse = await client.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
            Assert.Equal(HttpStatusCode.OK, initializeAttachmentResponse.StatusCode);
            var attachmentId = await initializeAttachmentResponse.Content.ReadFromJsonAsync<Guid>();
            var attachmentOverview = await (await client.GetAsync($"correspondence/api/v1/attachment/{attachmentId}")).Content.ReadFromJsonAsync<AttachmentOverviewExt>(responseSerializerOptions);
            Assert.Equal(AttachmentStatusExt.Initialized, attachmentOverview?.Status);
            return attachmentId;
        }
        public async static Task<Guid> GetPublishedAttachment(HttpClient client, JsonSerializerOptions responseSerializerOptions)
        {
            var attachment = new AttachmentBuilder().CreateAttachment().Build();
            var initializeAttachmentResponse = await client.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
            Assert.Equal(HttpStatusCode.OK, initializeAttachmentResponse.StatusCode);
            var attachmentId = await initializeAttachmentResponse.Content.ReadFromJsonAsync<Guid>();
            var uploadResponse = await UploadAttachment(attachmentId, client);
            Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
            var attachmentOverview = await WaitForAttachmentStatusUpdate(client, responseSerializerOptions, attachmentId, AttachmentStatusExt.Published);
            Assert.Equal(AttachmentStatusExt.Published, attachmentOverview?.Status);
            return attachmentId;
        }
        public async static Task<HttpResponseMessage> UploadAttachment(Guid? attachmentId, HttpClient client, ByteArrayContent? originalAttachmentData = null)
        {
            if (attachmentId is null)
            {
                Assert.Fail("AttachmentId is null");
            }
            var content = originalAttachmentData ?? new ByteArrayContent(Encoding.UTF8.GetBytes("This is the contents of the uploaded file"));
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

            var uploadResponse = await client.PostAsync($"correspondence/api/v1/attachment/{attachmentId}/upload", content);
            return uploadResponse;
        }
        public static string CalculateChecksum(byte[] data)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] hash = md5.ComputeHash(data);
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }
        public static async Task<AttachmentOverviewExt> WaitForAttachmentStatusUpdate(HttpClient client, JsonSerializerOptions responseSerializerOptions, Guid attachmentId, AttachmentStatusExt expectedStatus, int maxRetries = 5, int delayMs = 2500)
        {
            await Task.Delay(1000);
            for (int i = 0; i < maxRetries; i++)
            {
                var attachment = await client.GetFromJsonAsync<AttachmentOverviewExt>($"correspondence/api/v1/attachment/{attachmentId}", responseSerializerOptions);

                if (attachment?.Status == expectedStatus)
                {
                    return attachment;
                }

                if (attachment?.Status == AttachmentStatusExt.Failed)
                {
                    Assert.Fail($"Attachment failed with status: {attachment.StatusText}");
                }

                await Task.Delay(delayMs);
            }
            
            // Status didn't update within the expected time
            var finalAttachment = await client.GetFromJsonAsync<AttachmentOverviewExt>($"correspondence/api/v1/attachment/{attachmentId}", responseSerializerOptions);
            Assert.NotNull(finalAttachment);
            Assert.Fail($"Attachment status did not update to {expectedStatus} within {maxRetries * delayMs + 1000}ms. Current status: {finalAttachment?.Status}");
            return finalAttachment;
        }
    }
}
