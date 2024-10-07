using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Tests.Helpers;
using Microsoft.AspNetCore.Http;
namespace Altinn.Correspondence.Tests.Factories;
public static class AttachmentFactory
{
    public static InitializeAttachmentExt GetBasicAttachment() => new InitializeAttachmentExt()
    {
        ResourceId = "1",
        DataType = "html",
        Name = "Test file logical name",
        RestrictionName = "Test file restriction name",
        Sender = "0192:986252932",
        SendersReference = "1234",
        FileName = "test-file",
        IsEncrypted = false
    };
    public static InitializeCorrespondenceAttachmentExt GetAttachmentMetaData(string fileName, AttachmentOverviewExt existingAttachmentData = null)
    {
        var attachmentData = new InitializeCorrespondenceAttachmentExt()
        {
            DataType = existingAttachmentData?.DataType ?? "txt",
            Name = existingAttachmentData?.Name ?? fileName,
            RestrictionName = existingAttachmentData?.RestrictionName ?? "testFile3",
            SendersReference = existingAttachmentData?.SendersReference ?? "1234",
            FileName = existingAttachmentData?.FileName ?? fileName,
            IsEncrypted = existingAttachmentData?.IsEncrypted ?? false,
            Checksum = existingAttachmentData?.Checksum,
        };
        return attachmentData;
    }
    public static async Task<string> GetInitializedAttachment(HttpClient client, JsonSerializerOptions responseSerializerOptions)
    {
        var attachment = new AttachmentBuilder().CreateAttachment().Build();
        var initializeAttachmentResponse = await client.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
        Assert.Equal(HttpStatusCode.OK, initializeAttachmentResponse.StatusCode);
        var attachmentId = await initializeAttachmentResponse.Content.ReadAsStringAsync();
        var attachmentOverview = await (await client.GetAsync($"correspondence/api/v1/attachment/{attachmentId}")).Content.ReadFromJsonAsync<AttachmentOverviewExt>(responseSerializerOptions);
        Assert.Equal(AttachmentStatusExt.Initialized, attachmentOverview?.Status);
        return attachmentId;
    }
    public async static Task<string> GetPublishedAttachment(HttpClient client, JsonSerializerOptions responseSerializerOptions)
    {
        var attachment = new AttachmentBuilder().CreateAttachment().Build();
        var initializeAttachmentResponse = await client.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
        Assert.Equal(HttpStatusCode.OK, initializeAttachmentResponse.StatusCode);
        var attachmentId = await initializeAttachmentResponse.Content.ReadAsStringAsync();
        var uploadResponse = await AttachmentHelper.UploadAttachment(attachmentId, client);
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
        var attachmentOverview = await (await client.GetAsync($"correspondence/api/v1/attachment/{attachmentId}")).Content.ReadFromJsonAsync<AttachmentOverviewExt>(responseSerializerOptions);
        Assert.Equal(AttachmentStatusExt.Published, attachmentOverview?.Status);
        return attachmentId;
    }
}