using System.Net.Http.Json;
using System.Text.Json;
using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Tests.Factories;
using Microsoft.AspNetCore.Http;
namespace Altinn.Correspondence.Tests.Factories;
public static class AttachmentFactory
{
    public static InitializeCorrespondenceAttachmentExt GetAttachmentMetaData(FormFile file)
    {
        var attachmentData = new InitializeCorrespondenceAttachmentExt()
        {
            DataType = "text",
            Name = file.FileName,
            RestrictionName = "testFile3",
            SendersReference = "1234",
            FileName = file.FileName,
            IsEncrypted = false
        };
        return attachmentData;
    }
    public static async Task<string> GetInitializedAttachment(HttpClient client, JsonSerializerOptions responseSerializerOptions)
    {
        var initializeAttachmentResponse = await client.PostAsJsonAsync("correspondence/api/v1/attachment", InitializeAttachmentFactory.BasicAttachment());
        var attachmentId = await initializeAttachmentResponse.Content.ReadAsStringAsync();
        var attachmentOverview = await (await client.GetAsync($"correspondence/api/v1/attachment/{attachmentId}")).Content.ReadFromJsonAsync<AttachmentOverviewExt>(responseSerializerOptions);
        Assert.Equal(AttachmentStatusExt.Initialized, attachmentOverview?.Status);
        return attachmentId;
    }
    public async static Task<string> GetPublishedAttachment(HttpClient client, JsonSerializerOptions responseSerializerOptions)
    {
        var initializeAttachmentResponse = await client.PostAsJsonAsync("correspondence/api/v1/attachment", InitializeAttachmentFactory.BasicAttachment());
        var attachmentId = await initializeAttachmentResponse.Content.ReadAsStringAsync();
        await UploadAttachment(attachmentId, client);
        var attachmentOverview = await (await client.GetAsync($"correspondence/api/v1/attachment/{attachmentId}")).Content.ReadFromJsonAsync<AttachmentOverviewExt>(responseSerializerOptions);
        Assert.Equal(AttachmentStatusExt.Published, attachmentOverview?.Status);
        return attachmentId;
    }

    private async static Task<HttpResponseMessage> UploadAttachment(string? attachmentId, HttpClient client, ByteArrayContent? originalAttachmentData = null)
    {
        if (attachmentId == null)
        {
            Assert.Fail("AttachmentId is null");
        }
        var data = originalAttachmentData ?? new ByteArrayContent([1, 2, 3, 4]);

        var uploadResponse = await client.PostAsync($"correspondence/api/v1/attachment/{attachmentId}/upload", data);
        return uploadResponse;
    }
}