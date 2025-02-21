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
            var attachmentOverview = await (await client.GetAsync($"correspondence/api/v1/attachment/{attachmentId}")).Content.ReadFromJsonAsync<AttachmentOverviewExt>(responseSerializerOptions);
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
    }
}
