using Altinn.Correspondece.Tests.Factories;
using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.GetCorrespondences;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Altinn.Correspondence.Tests;

public class CorrespondenceControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _responseSerializerOptions;
    private readonly ITestOutputHelper _output;



    public CorrespondenceControllerTests(CustomWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _client = _factory.CreateClientInternal();
        _responseSerializerOptions = new JsonSerializerOptions(new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        });
        _responseSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        _output = output;
    }

    [Fact]
    public async Task InitializeCorrespondence()
    {
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondence());
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task InitializeCorrespondence_With_HTML_Or_Markdown_In_Title_fails()
    {
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondenceWithHtmlInTitle());
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);

        initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondenceWithMarkdownInTitle());
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
    }
    [Fact]
    public async Task InitializeCorrespondence_With_HTML_In_Summary_Or_Body_fails()
    {
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondenceWithHtmlInSummary());
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);

        initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondenceWithHtmlInBody());
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
    }
    [Fact]
    public async Task InitializeCorrespondence_No_Message_Body_fails()
    {
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondenceWithNoMessageBody());
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
    }
    [Fact]
    public async Task InitializeCorrespondence_With_Different_Markdown_In_Body()
    {
        var correspondence = InitializeCorrespondenceFactory.BasicCorrespondence();
        correspondence.Content.MessageBody = File.ReadAllText("Data/Markdown.text");
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task InitializeCorrespondence_Recipient_Can_Handle_Org_And_Ssn()
    {
        var correspondence = InitializeCorrespondenceFactory.BasicCorrespondence();
        correspondence.Recipient = "1234:123456789";
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();

        correspondence.Recipient = "12345678901";
        initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task InitializeCorrespondence_With_Invalid_Sender_Returns_BadRequest()
    {
        var correspondence = InitializeCorrespondenceFactory.BasicCorrespondence();
        correspondence.Sender = "invalid-sender";
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);

        correspondence.Sender = "123456789";
        initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task InitializeCorrespondence_With_Invalid_Recipient_Returns_BadRequest()
    {
        var correspondence = InitializeCorrespondenceFactory.BasicCorrespondence();
        correspondence.Recipient = "invalid-recipient";
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);

        correspondence.Recipient = "123456789";
        initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);

        correspondence.Recipient = "1234567890123";
        initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task UploadCorrespondence_Gives_Ok()
    {
        var correspondence = InitializeCorrespondenceFactory.BasicCorrespondence();
        using (var stream = System.IO.File.OpenRead("./Data/Markdown.text"))
        {
            var file = new FormFile(stream, 0, stream.Length, null, Path.GetFileName(stream.Name));
            var attachmentData = new InitializeCorrespondenceAttachmentExt()
            {
                DataType = "text",
                Name = file.FileName,
                RestrictionName = "testFile3",
                SendersReference = "1234",
                FileName = file.FileName,
                IsEncrypted = false,
            };
            correspondence.Content.Attachments = new List<InitializeCorrespondenceAttachmentExt>() { attachmentData };
            var formData = CorrespondenceToFormData(correspondence);
            using var fileStream = file.OpenReadStream();
            formData.Add(new StreamContent(fileStream), "attachments", file.FileName);
            var uploadCorrespondenceResponse = await _client.PostAsync("correspondence/api/v1/correspondence/upload", formData);
            uploadCorrespondenceResponse.EnsureSuccessStatusCode();

            var response = await uploadCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondenceResponseExt>(_responseSerializerOptions);
            var attachmentId = response?.AttachmentIds.FirstOrDefault();
            var attachmentOverview = await _client.GetFromJsonAsync<AttachmentOverviewExt>($"correspondence/api/v1/attachment/{attachmentId}", _responseSerializerOptions);
            Assert.NotNull(attachmentOverview.DataLocationUrl);
            correspondence.Content.Attachments.Add(new InitializeCorrespondenceAttachmentExt()
            {
                DataLocationUrl = attachmentOverview.DataLocationUrl,
                DataType = attachmentOverview.DataType,
                FileName = attachmentOverview.FileName,
                Name = attachmentOverview.Name,
                RestrictionName = attachmentOverview.RestrictionName,
                SendersReference = attachmentOverview.SendersReference,
                IsEncrypted = attachmentOverview.IsEncrypted,
                Checksum = attachmentOverview.Checksum
            });
            formData = CorrespondenceToFormData(correspondence);
            formData.Add(new StreamContent(fileStream), "attachments", file.FileName);
            var uploadCorrespondenceResponse2 = await _client.PostAsync("correspondence/api/v1/correspondence/upload", formData);
            uploadCorrespondenceResponse2.EnsureSuccessStatusCode();
        }
    }
    [Fact]
    public async Task UploadCorrespondence_With_Multiple_Files()
    {
        var correspondence = InitializeCorrespondenceFactory.BasicCorrespondence();

        using var stream = System.IO.File.OpenRead("./Data/Markdown.text");
        var file = new FormFile(stream, 0, stream.Length, null, Path.GetFileName(stream.Name));
        using var fileStream = file.OpenReadStream();
        using var stream2 = System.IO.File.OpenRead("./Data/test.txt");
        var file2 = new FormFile(stream2, 0, stream2.Length, null, Path.GetFileName(stream2.Name));
        using var fileStream2 = file2.OpenReadStream();

        correspondence.Content.Attachments = new List<InitializeCorrespondenceAttachmentExt>(){
            new InitializeCorrespondenceAttachmentExt(){
                DataType = "text",
                Name = file.FileName,
                RestrictionName = "testFile3",
                SendersReference = "1234",
                FileName = file.FileName,
                IsEncrypted = false,
            },
             new InitializeCorrespondenceAttachmentExt(){
                DataType = "text",
                Name = file2.FileName,
                RestrictionName = "testFile3",
                SendersReference = "1234",
                FileName = file2.FileName,
                IsEncrypted = false,
            }};
        var formData = CorrespondenceToFormData(correspondence);
        formData.Add(new StreamContent(fileStream), "attachments", file.FileName);
        formData.Add(new StreamContent(fileStream2), "attachments", file2.FileName);


        var uploadCorrespondenceResponse = await _client.PostAsync("correspondence/api/v1/correspondence/upload", formData);
        uploadCorrespondenceResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task UploadCorrespondence_No_Files_Gives_Bad_request()
    {
        var correspondence = InitializeCorrespondenceFactory.BasicCorrespondence();
        correspondence.Content.Attachments = new List<InitializeCorrespondenceAttachmentExt>() { };
        var formData = CorrespondenceToFormData(correspondence);
        var uploadCorrespondenceResponse = await _client.PostAsync("correspondence/api/v1/correspondence/upload", formData);
        Assert.Equal(HttpStatusCode.BadRequest, uploadCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task GetCorrespondences()
    {
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondence());
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());

        var initializeCorrespondenceResponse2 = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondence());
        Assert.True(initializeCorrespondenceResponse2.IsSuccessStatusCode, await initializeCorrespondenceResponse2.Content.ReadAsStringAsync());

        var correspondenceList = await _client.GetFromJsonAsync<GetCorrespondencesResponse>("correspondence/api/v1/correspondence?resourceId=1&offset=0&limit=10&status=0");
        Assert.True(correspondenceList?.Pagination.TotalItems > 0);
    }

    [Fact]
    public async Task GetCorrespondencesOnlyFromSearchedResourceId()
    {
        var resourceA = Guid.NewGuid().ToString();
        var resourceB = Guid.NewGuid().ToString();
        var correspondenceForResourceA = InitializeCorrespondenceFactory.BasicCorrespondence();
        correspondenceForResourceA.ResourceId = resourceA;
        var correspondenceForResourceB = InitializeCorrespondenceFactory.BasicCorrespondence();
        correspondenceForResourceB.ResourceId = resourceB;

        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondenceForResourceA);
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());

        var initializeCorrespondenceResponse2 = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondenceForResourceB);
        Assert.True(initializeCorrespondenceResponse2.IsSuccessStatusCode, await initializeCorrespondenceResponse2.Content.ReadAsStringAsync());

        var correspondenceList = await _client.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={resourceA}&offset=0&limit=10&status=0");
        Assert.True(correspondenceList?.Pagination.TotalItems == 1);
    }

    [Fact]
    public async Task GetCorrespondenceOverview()
    {
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondence());
        var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondenceResponseExt>();
        var getCorrespondenceOverviewResponse = await _client.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.CorrespondenceId}");
        Assert.True(getCorrespondenceOverviewResponse.IsSuccessStatusCode, await getCorrespondenceOverviewResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetCorrespondenceDetails()
    {
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondence());
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
        var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondenceResponseExt>();
        var getCorrespondenceOverviewResponse = await _client.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.CorrespondenceId}/details");
        Assert.True(getCorrespondenceOverviewResponse.IsSuccessStatusCode, await getCorrespondenceOverviewResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task MarkActions_CorrespondenceNotExists_ReturnNotFound()
    {
        var readResponse = await _client.PostAsync($"correspondence/api/v1/correspondence/00000000-0100-0000-0000-000000000000/markasread", null);
        Assert.Equal(HttpStatusCode.NotFound, readResponse.StatusCode);

        var confirmResponse = await _client.PostAsync($"correspondence/api/v1/correspondence/00000000-0100-0000-0000-000000000000/confirm", null);
        Assert.Equal(HttpStatusCode.NotFound, confirmResponse.StatusCode);

        var archiveResponse = await _client.PostAsync($"correspondence/api/v1/correspondence/00000000-0100-0000-0000-000000000000/archive", null);
        Assert.Equal(HttpStatusCode.NotFound, archiveResponse.StatusCode);
    }

    [Fact]
    public async Task ReceiverMarkActions_CorrespondenceNotPublished_ReturnBadRequest()
    {
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondence());
        var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondenceResponseExt>();
        Assert.NotNull(correspondence);
        var readResponse = await _client.PostAsync($"correspondence/api/v1/correspondence/{correspondence.CorrespondenceId}/markasread", null);
        Assert.Equal(HttpStatusCode.BadRequest, readResponse.StatusCode);

        var confirmResponse = await _client.PostAsync($"correspondence/api/v1/correspondence/{correspondence.CorrespondenceId}/confirm", null);
        Assert.Equal(HttpStatusCode.BadRequest, confirmResponse.StatusCode);
    }

    [Fact]
    public async Task ReceiverMarkActions_CorrespondencePublished_ReturnOk()
    {
        var uploadedAttachment = await InitializeAttachment();
        Assert.NotNull(uploadedAttachment);
        var correspondence = InitializeCorrespondenceFactory.BasicCorrespondenceWithFileAttachment(uploadedAttachment.DataLocationUrl);
        correspondence.VisibleFrom = DateTime.UtcNow.AddMinutes(-1);
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence, _responseSerializerOptions);
        var response = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondenceResponseExt>();
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
        Assert.NotNull(response);

        var overview = await _client.GetFromJsonAsync<CorrespondenceOverviewExt>($"correspondence/api/v1/correspondence/{response.CorrespondenceId}", _responseSerializerOptions);
        Assert.True(overview?.Status == CorrespondenceStatusExt.Published);
        Assert.NotNull(correspondence);
        var readResponse = await _client.PostAsync($"correspondence/api/v1/correspondence/{response.CorrespondenceId}/markasread", null);
        readResponse.EnsureSuccessStatusCode();

        var confirmResponse = await _client.PostAsync($"correspondence/api/v1/correspondence/{response.CorrespondenceId}/confirm", null);
        confirmResponse.EnsureSuccessStatusCode();

        var markAsUnreadResponse = await _client.PostAsync($"correspondence/api/v1/correspondence/{response.CorrespondenceId}/markasunread", null);
        markAsUnreadResponse.EnsureSuccessStatusCode();
        overview = await _client.GetFromJsonAsync<CorrespondenceOverviewExt>($"correspondence/api/v1/correspondence/{response.CorrespondenceId}", _responseSerializerOptions);
        Assert.True(overview?.MarkedUnread == true);
    }

    [Fact]
    public async Task Correspondence_with_dataLocationUrl_Reuses_Attachment()
    {
        var uploadedAttachment = await InitializeAttachment();
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondenceWithFileAttachment(uploadedAttachment.DataLocationUrl), _responseSerializerOptions);
        var response = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondenceResponseExt>();
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
        Assert.Equal(uploadedAttachment.AttachmentId.ToString(), response?.AttachmentIds?.FirstOrDefault().ToString());
    }

    [Fact]
    public async Task Delete_Initialized_Correspondence_Gives_OK()
    {
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondence());
        var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondenceResponseExt>();
        Assert.NotNull(correspondence);
        var response = await _client.DeleteAsync($"correspondence/api/v1/correspondence/{correspondence.CorrespondenceId}/purge");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var overview = await _client.GetFromJsonAsync<CorrespondenceOverviewExt>($"correspondence/api/v1/correspondence/{correspondence.CorrespondenceId}", _responseSerializerOptions);
        Assert.Equal(overview?.Status, CorrespondenceStatusExt.PurgedByRecipient);
    }

    [Fact]
    public async Task Delete_Correspondence_Also_deletes_attachment()
    {
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondence());
        var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondenceResponseExt>();
        Assert.NotNull(correspondence);
        var response = await _client.DeleteAsync($"correspondence/api/v1/correspondence/{correspondence.CorrespondenceId}/purge");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var attachment = await _client.GetFromJsonAsync<AttachmentOverviewExt>($"correspondence/api/v1/attachment/{correspondence.AttachmentIds.FirstOrDefault()}", _responseSerializerOptions);
        Assert.Equal(attachment?.Status, AttachmentStatusExt.Purged);
    }

    [Fact]
    public async Task Delete_correspondence_dont_delete_attachment_with_multiple_correspondences()
    {
        var attachment = await InitializeAttachment();
        Assert.NotNull(attachment);
        var correspondence1 = InitializeCorrespondenceFactory.BasicCorrespondenceWithFileAttachment(attachment.DataLocationUrl);
        var correspondence2 = InitializeCorrespondenceFactory.BasicCorrespondenceWithFileAttachment(attachment.DataLocationUrl);

        var initializeCorrespondenceResponse1 = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence1, _responseSerializerOptions);
        var response1 = await initializeCorrespondenceResponse1.Content.ReadFromJsonAsync<InitializeCorrespondenceResponseExt>();
        initializeCorrespondenceResponse1.EnsureSuccessStatusCode();
        Assert.NotNull(response1);

        var initializeCorrespondenceResponse2 = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence2, _responseSerializerOptions);
        var response2 = await initializeCorrespondenceResponse2.Content.ReadFromJsonAsync<InitializeCorrespondenceResponseExt>();
        initializeCorrespondenceResponse2.EnsureSuccessStatusCode();
        Assert.NotNull(response2);

        var deleteResponse = await _client.DeleteAsync($"correspondence/api/v1/correspondence/{response1.CorrespondenceId}/purge");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var attachmentOverview = await _client.GetFromJsonAsync<AttachmentOverviewExt>($"correspondence/api/v1/attachment/{response1.AttachmentIds.FirstOrDefault()}", _responseSerializerOptions);
        Assert.NotEqual(attachmentOverview?.Status, AttachmentStatusExt.Purged);
    }

    [Fact]
    public async Task Delete_NonExisting_Correspondence_Gives_NotFound()
    {
        var deleteResponse = await _client.DeleteAsync($"correspondence/api/v1/correspondence/00000000-0100-0000-0000-000000000000/purge");
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_Published_Correspondences_As_Sender_Fails()
    {
        //TODO: When we implement sender
        Assert.True(true);
    }
    [Fact]
    public async Task Delete_Initialized_Correspondences_As_Receiver_Fails()
    {
        //TODO: When we implement Receiver
        Assert.True(true);
    }

    private async Task<HttpResponseMessage> UploadAttachment(string? attachmentId, ByteArrayContent? originalAttachmentData = null)
    {
        if (attachmentId == null)
        {
            Assert.Fail("AttachmentId is null");
        }
        var data = originalAttachmentData ?? new ByteArrayContent(new byte[] { 1, 2, 3, 4 });

        var uploadResponse = await _client.PostAsync($"correspondence/api/v1/attachment/{attachmentId}/upload", data);
        return uploadResponse;
    }
    private async Task<AttachmentOverviewExt?> InitializeAttachment()
    {
        var attachment = InitializeAttachmentFactory.BasicAttachment();
        var initializeResponse = await _client.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
        initializeResponse.EnsureSuccessStatusCode();
        var attachmentId = await initializeResponse.Content.ReadAsStringAsync();
        var overview = await (await UploadAttachment(attachmentId)).Content.ReadFromJsonAsync<AttachmentOverviewExt>(_responseSerializerOptions);
        return overview;
    }
    private MultipartFormDataContent CorrespondenceToFormData(InitializeCorrespondenceExt correspondence)
    {
        var formData = new MultipartFormDataContent();
        formData.Add(new StringContent(correspondence.ResourceId), "resourceId");
        formData.Add(new StringContent(correspondence.Sender), "sender");
        formData.Add(new StringContent(correspondence.SendersReference), "sendersReference");
        formData.Add(new StringContent(correspondence.Recipient), "recipient");
        formData.Add(new StringContent(correspondence.VisibleFrom.ToString()), "visibleFrom");
        formData.Add(new StringContent(correspondence.AllowSystemDeleteAfter.ToString()), "AllowSystemDeleteAfter");
        formData.Add(new StringContent(correspondence.Content.MessageTitle), "content.MessageTitle");
        formData.Add(new StringContent(correspondence.Content.MessageSummary), "content.MessageSummary");
        formData.Add(new StringContent(correspondence.Content.MessageBody), "content.MessageBody");
        formData.Add(new StringContent(correspondence.Content.Language), "content.Language");
        formData.Add(new StringContent(correspondence.IsReservable.ToString()), "isReservable");
        var i = 0;
        foreach (var attachment in correspondence.Content.Attachments)
        {
            formData.Add(new StringContent(attachment.DataLocationType.ToString()), "content.Attachments[" + i + "].DataLocationType");
            formData.Add(new StringContent(attachment.DataType), "content.Attachments[" + i + "].DataType");
            formData.Add(new StringContent(attachment.Name), "content.Attachments[" + i + "].Name");
            formData.Add(new StringContent(attachment.RestrictionName), "content.Attachments[" + i + "].RestrictionName");
            formData.Add(new StringContent(attachment.SendersReference), "content.Attachments[" + i + "].SendersReference");
            formData.Add(new StringContent(attachment.IsEncrypted.ToString()), "content.Attachments[" + i + "].IsEncrypted");
            i++;
        }
        i = 0;
        foreach (var externalReference in correspondence.ExternalReferences)
        {
            formData.Add(new StringContent(externalReference.ReferenceType.ToString()), "externalReferences[" + i + "].referenceType");
            formData.Add(new StringContent(externalReference.ReferenceValue), "externalReferences[" + i + "].referenceValue");
            i++;
        }
        i = 0;
        foreach (var replyOption in correspondence.ReplyOptions)
        {
            formData.Add(new StringContent(replyOption.LinkURL), "replyOptions[" + i + "].linkURL");
            formData.Add(new StringContent(replyOption.LinkText), "replyOptions[" + i + "].linkText");
            i++;
        }
        i = 0;
        foreach (var notification in correspondence.Notifications)
        {
            formData.Add(new StringContent(notification.NotificationTemplate), "notifications[" + i + "].notificationTemplate");
            formData.Add(new StringContent(notification.CustomTextToken), "notifications[" + i + "].customTextToken");
            formData.Add(new StringContent(notification.SendersReference), "notifications[" + i + "].sendersReference");
            formData.Add(new StringContent(notification.RequestedSendTime.ToString()), "notifications[" + i + "].requestedSendTime");
            i++;
        }
        foreach (var propertyList in correspondence.PropertyList)
        {
            formData.Add(new StringContent(propertyList.Value), "propertyLists." + propertyList.Key);
        }
        return formData;
    }
}