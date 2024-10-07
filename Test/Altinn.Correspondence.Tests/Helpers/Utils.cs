using System.Text;

namespace Altinn.Correspondence.Tests.Helpers
{
    internal static class AttachmentHelper
    {

        public async static Task<HttpResponseMessage> UploadAttachment(string? attachmentId, HttpClient client, ByteArrayContent? originalAttachmentData = null)
        {
            if (attachmentId == null)
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