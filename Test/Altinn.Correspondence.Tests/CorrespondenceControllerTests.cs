using Altinn.Correspondece.Tests.Factories;
using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.GetCorrespondences;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Altinn.Correspondence.Tests;

public class CorrespondenceControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _responseSerializerOptions;

    public CorrespondenceControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClientInternal();
        _responseSerializerOptions = new JsonSerializerOptions(new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        });
        _responseSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    }

    [Fact]
    public async Task InitializeCorrespondence()
    {
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondences());
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
        var payload = InitializeCorrespondenceFactory.BasicCorrespondences();
        payload.Correspondence.Content.MessageBody = File.ReadAllText("Data/Markdown.text");
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task InitializeCorrespondence_Recipient_Can_Handle_Org_And_Ssn()
    {
        var payload = InitializeCorrespondenceFactory.BasicCorrespondences();
        payload.Recipients = new List<string> { "1234:123456789" };
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();

        payload.Recipients = new List<string> { "12345678901" };
        initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task InitializeCorrespondence_With_Invalid_Sender_Returns_BadRequest()
    {
        var payload = InitializeCorrespondenceFactory.BasicCorrespondences();
        payload.Correspondence.Sender = "invalid-sender";
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);

        payload.Correspondence.Sender = "123456789";
        initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task InitializeCorrespondence_With_Invalid_Recipient_Returns_BadRequest()
    {
        var payload = InitializeCorrespondenceFactory.BasicCorrespondences();
        payload.Recipients = new List<string> { "invalid-recipient" };
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);

        payload.Recipients = new List<string> { "123456789" };
        initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);

        payload.Recipients = new List<string> { "1234567812390123" };
        initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task UploadCorrespondence_Gives_Ok()
    {
        var payload = InitializeCorrespondenceFactory.BasicCorrespondences();
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
                IsEncrypted = false
            };
            payload.Correspondence.Content.Attachments = new List<InitializeCorrespondenceAttachmentExt>() { attachmentData };
            var formData = CorrespondenceToFormData(payload.Correspondence);
            formData.Add(new StringContent("0192:986252932"), "recipients[0]");
            using var fileStream = file.OpenReadStream();
            formData.Add(new StreamContent(fileStream), "attachments", file.FileName);
            var uploadCorrespondenceResponse = await _client.PostAsync("correspondence/api/v1/correspondence/upload", formData);
            uploadCorrespondenceResponse.EnsureSuccessStatusCode();

            var response = await uploadCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            var attachmentId = response?.AttachmentIds.FirstOrDefault();
            var attachmentOverview = await _client.GetFromJsonAsync<AttachmentOverviewExt>($"correspondence/api/v1/attachment/{attachmentId}", _responseSerializerOptions);
            Assert.NotNull(attachmentOverview.DataLocationUrl);
            payload.Correspondence.Content.Attachments.Add(new InitializeCorrespondenceAttachmentExt()
            {
                DataLocationUrl = attachmentOverview.DataLocationUrl,
                DataType = attachmentOverview.DataType,
                FileName = attachmentOverview.FileName,
                Name = "Logical file name",
                RestrictionName = attachmentOverview.RestrictionName,
                SendersReference = attachmentOverview.SendersReference,
                IsEncrypted = attachmentOverview.IsEncrypted,
                Checksum = attachmentOverview.Checksum
            });
            formData = CorrespondenceToFormData(payload.Correspondence);
            formData.Add(new StreamContent(fileStream), "attachments", file.FileName);
            var uploadCorrespondenceResponse2 = await _client.PostAsync("correspondence/api/v1/correspondence/upload", formData);
            Assert.True(uploadCorrespondenceResponse.IsSuccessStatusCode, await uploadCorrespondenceResponse.Content.ReadAsStringAsync());
        }
    }
    [Fact]
    public async Task UploadCorrespondence_With_Multiple_Files()
    {
        var payload = InitializeCorrespondenceFactory.BasicCorrespondences();

        using var stream = System.IO.File.OpenRead("./Data/Markdown.text");
        var file = new FormFile(stream, 0, stream.Length, null, Path.GetFileName(stream.Name));
        using var fileStream = file.OpenReadStream();
        using var stream2 = System.IO.File.OpenRead("./Data/test.txt");
        var file2 = new FormFile(stream2, 0, stream2.Length, null, Path.GetFileName(stream2.Name));
        using var fileStream2 = file2.OpenReadStream();

        payload.Correspondence.Content.Attachments = new List<InitializeCorrespondenceAttachmentExt>(){
            new InitializeCorrespondenceAttachmentExt(){
                DataType = "text",
                Name = "markdown example",
                RestrictionName = "testFile3",
                SendersReference = "1234",
                FileName = file.FileName,
                IsEncrypted = false,
            },
             new InitializeCorrespondenceAttachmentExt(){
                DataType = "text",
                Name = "test file",
                RestrictionName = "testFile3",
                SendersReference = "1234",
                FileName = file2.FileName,
                IsEncrypted = false,
            }};
        var formData = CorrespondenceToFormData(payload.Correspondence);
        formData.Add(new StringContent("0192:986252932"), "recipients[0]");
        formData.Add(new StreamContent(fileStream), "attachments", file.FileName);
        formData.Add(new StreamContent(fileStream2), "attachments", file2.FileName);

        var uploadCorrespondenceResponse = await _client.PostAsync("correspondence/api/v1/correspondence/upload", formData);
        Assert.True(uploadCorrespondenceResponse.IsSuccessStatusCode, await uploadCorrespondenceResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UploadCorrespondence_No_Files_Gives_Bad_request()
    {
        var payload = InitializeCorrespondenceFactory.BasicCorrespondences();
        payload.Correspondence.Content.Attachments = new List<InitializeCorrespondenceAttachmentExt>() { };
        var formData = CorrespondenceToFormData(payload.Correspondence);
        var uploadCorrespondenceResponse = await _client.PostAsync("correspondence/api/v1/correspondence/upload", formData);
        Assert.Equal(HttpStatusCode.BadRequest, uploadCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task InitializeCorrespondences()
    {
        var uploadedAttachment = await InitializeAttachment();
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondences(uploadedAttachment.DataLocationUrl));
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UploadCorrespondences_With_Multiple_Files()
    {

        var payload = InitializeCorrespondenceFactory.BasicCorrespondences();
        using var stream = System.IO.File.OpenRead("./Data/Markdown.text");
        var file = new FormFile(stream, 0, stream.Length, null, Path.GetFileName(stream.Name));
        using var fileStream = file.OpenReadStream();
        using var stream2 = System.IO.File.OpenRead("./Data/test.txt");
        var file2 = new FormFile(stream2, 0, stream2.Length, null, Path.GetFileName(stream2.Name));
        using var fileStream2 = file2.OpenReadStream();
        payload.Correspondence.Content.Attachments = new List<InitializeCorrespondenceAttachmentExt>(){
            new InitializeCorrespondenceAttachmentExt(){
                DataType = "text",
                Name = "MARKDOWN EXAMPLE",
                RestrictionName = "testFile3",
                SendersReference = "1234",
                FileName = file.FileName,
                IsEncrypted = false,
            },
             new InitializeCorrespondenceAttachmentExt(){
                DataType = "text",
                Name = "test file",
                RestrictionName = "testFile3",
                SendersReference = "1234",
                FileName = file2.FileName,
                IsEncrypted = false,
            }};

        var formData = CorrespondenceToFormData(payload.Correspondence);

        formData.Add(new StreamContent(fileStream), "attachments", file.FileName);
        formData.Add(new StreamContent(fileStream2), "attachments", file2.FileName);
        formData.Add(new StringContent("0192:986252932"), "recipients[0]");
        formData.Add(new StringContent("0198:991234649"), "recipients[1]");

        var uploadCorrespondenceResponse = await _client.PostAsync("correspondence/api/v1/correspondence/upload", formData);
        Assert.True(uploadCorrespondenceResponse.IsSuccessStatusCode, await uploadCorrespondenceResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetCorrespondences()
    {
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondences());
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());

        var initializeCorrespondenceResponse2 = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondences());
        Assert.True(initializeCorrespondenceResponse2.IsSuccessStatusCode, await initializeCorrespondenceResponse2.Content.ReadAsStringAsync());

        var correspondenceList = await _client.GetFromJsonAsync<GetCorrespondencesResponse>("correspondence/api/v1/correspondence?resourceId=1&offset=0&limit=10&status=0");
        Assert.True(correspondenceList?.Pagination.TotalItems > 0);
    }

    [Fact]
    public async Task GetCorrespondencesOnlyFromSearchedResourceId()
    {
        var resourceA = Guid.NewGuid().ToString();
        var resourceB = Guid.NewGuid().ToString();
        var payloadForResourceA = InitializeCorrespondenceFactory.BasicCorrespondences();
        payloadForResourceA.Correspondence.ResourceId = resourceA;
        var payloadForResourceB = InitializeCorrespondenceFactory.BasicCorrespondences();
        payloadForResourceB.Correspondence.ResourceId = resourceB;

        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", payloadForResourceA);
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());

        var initializeCorrespondenceResponse2 = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", payloadForResourceB);
        Assert.True(initializeCorrespondenceResponse2.IsSuccessStatusCode, await initializeCorrespondenceResponse2.Content.ReadAsStringAsync());

        var correspondenceList = await _client.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={resourceA}&offset=0&limit=10&status=0");
        Assert.Equal(correspondenceList?.Pagination.TotalItems, payloadForResourceA.Recipients.Count);
    }

    [Fact]
    public async Task GetCorrespondenceOverview()
    {
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondences());
        var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();
        var getCorrespondenceOverviewResponse = await _client.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.CorrespondenceIds.FirstOrDefault()}");
        Assert.True(getCorrespondenceOverviewResponse.IsSuccessStatusCode, await getCorrespondenceOverviewResponse.Content.ReadAsStringAsync());
        var response = await getCorrespondenceOverviewResponse.Content.ReadFromJsonAsync<CorrespondenceOverviewExt>(_responseSerializerOptions);
        Assert.Equal(response.Status, CorrespondenceStatusExt.Initialized);
    }
    [Fact]
    public async Task GetCorrespondenceOverview_WhenPublished_BecomesFetched()
    {
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondenceWithoutAttachments());
        var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();
        var getCorrespondenceOverviewResponse = await _client.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.CorrespondenceIds.FirstOrDefault()}");
        Assert.True(getCorrespondenceOverviewResponse.IsSuccessStatusCode, await getCorrespondenceOverviewResponse.Content.ReadAsStringAsync());
        var response = await getCorrespondenceOverviewResponse.Content.ReadFromJsonAsync<CorrespondenceOverviewExt>(_responseSerializerOptions);
        Assert.Equal(response.Status, CorrespondenceStatusExt.Fetched);
    }

    [Fact]
    public async Task GetCorrespondenceDetails()
    {
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondences());
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
        var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();
        var getCorrespondenceDetailsResponse = await _client.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.CorrespondenceIds.FirstOrDefault()}/details");
        Assert.True(getCorrespondenceDetailsResponse.IsSuccessStatusCode, await getCorrespondenceDetailsResponse.Content.ReadAsStringAsync());
        var response = await getCorrespondenceDetailsResponse.Content.ReadFromJsonAsync<CorrespondenceDetailsExt>(_responseSerializerOptions);
        Assert.Equal(response.Status, CorrespondenceStatusExt.Initialized);
    }
    [Fact]
    public async Task GetCorrespondenceDetails_WhenPublished_BecomesFetched()
    {
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondenceWithoutAttachments());
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
        var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();
        var getCorrespondenceDetailsResponse = await _client.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.CorrespondenceIds.FirstOrDefault()}/details");
        Assert.True(getCorrespondenceDetailsResponse.IsSuccessStatusCode, await getCorrespondenceDetailsResponse.Content.ReadAsStringAsync());
        var response = await getCorrespondenceDetailsResponse.Content.ReadFromJsonAsync<CorrespondenceDetailsExt>(_responseSerializerOptions);
        Assert.Equal(response.Status, CorrespondenceStatusExt.Fetched);
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
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondences());
        var correspondenceResponse = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();
        Assert.NotNull(correspondenceResponse);
        var readResponse = await _client.PostAsync($"correspondence/api/v1/correspondence/{correspondenceResponse?.CorrespondenceIds.FirstOrDefault()}/markasread", null);
        Assert.Equal(HttpStatusCode.BadRequest, readResponse.StatusCode);

        var confirmResponse = await _client.PostAsync($"correspondence/api/v1/correspondence/{correspondenceResponse?.CorrespondenceIds.FirstOrDefault()}/confirm", null);
        Assert.Equal(HttpStatusCode.BadRequest, confirmResponse.StatusCode);
    }

    [Fact]
    public async Task ReceiverMarkActions_CorrespondencePublished_ReturnOk()
    {
        var uploadedAttachment = await InitializeAttachment();
        Assert.NotNull(uploadedAttachment);
        var payload = InitializeCorrespondenceFactory.BasicCorrespondences();
        payload.ExistingAttachments = new List<Guid> { uploadedAttachment.AttachmentId };
        payload.Correspondence.Content.Attachments = new List<InitializeCorrespondenceAttachmentExt>();
        payload.Correspondence.VisibleFrom = DateTime.UtcNow.AddMinutes(-1);
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
        var response = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
        Assert.NotNull(response);

        var overview = await _client.GetFromJsonAsync<CorrespondenceOverviewExt>($"correspondence/api/v1/correspondence/{response.CorrespondenceIds.FirstOrDefault()}", _responseSerializerOptions);
        Assert.True(overview?.Status == CorrespondenceStatusExt.Fetched);
        Assert.NotNull(payload);
        var readResponse = await _client.PostAsync($"correspondence/api/v1/correspondence/{response.CorrespondenceIds.FirstOrDefault()}/markasread", null);
        readResponse.EnsureSuccessStatusCode();

        var confirmResponse = await _client.PostAsync($"correspondence/api/v1/correspondence/{response.CorrespondenceIds.FirstOrDefault()}/confirm", null);
        confirmResponse.EnsureSuccessStatusCode();

        var markAsUnreadResponse = await _client.PostAsync($"correspondence/api/v1/correspondence/{response.CorrespondenceIds.FirstOrDefault()}/markasunread", null);
        markAsUnreadResponse.EnsureSuccessStatusCode();
        overview = await _client.GetFromJsonAsync<CorrespondenceOverviewExt>($"correspondence/api/v1/correspondence/{response.CorrespondenceIds.FirstOrDefault()}", _responseSerializerOptions);
        Assert.True(overview?.MarkedUnread == true);
    }

    [Fact]
    public async Task Correspondence_with_dataLocationUrl_Reuses_Attachment()
    {
        var uploadedAttachment = await InitializeAttachment();
        var payload = InitializeCorrespondenceFactory.BasicCorrespondences();
        payload.ExistingAttachments = new List<Guid> { uploadedAttachment.AttachmentId };
        payload.Correspondence.Content.Attachments = new List<InitializeCorrespondenceAttachmentExt>();
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
        var response = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
        Assert.Equal(uploadedAttachment.AttachmentId.ToString(), response?.AttachmentIds?.FirstOrDefault().ToString());
    }

    [Fact]
    public async Task Delete_Initialized_Correspondence_Gives_OK()
    {
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondences());
        var correspondenceResponse = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();
        Assert.NotNull(correspondenceResponse);
        var response = await _client.DeleteAsync($"correspondence/api/v1/correspondence/{correspondenceResponse.CorrespondenceIds.FirstOrDefault()}/purge");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var overview = await _client.GetFromJsonAsync<CorrespondenceOverviewExt>($"correspondence/api/v1/correspondence/{correspondenceResponse.CorrespondenceIds.FirstOrDefault()}", _responseSerializerOptions);
        Assert.Equal(overview?.Status, CorrespondenceStatusExt.PurgedByRecipient);
    }

    [Fact]
    public async Task Delete_Correspondence_Also_deletes_attachment()
    {
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondences());
        var correspondenceResponse = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();
        Assert.NotNull(correspondenceResponse);
        foreach (var correspondenceId in correspondenceResponse.CorrespondenceIds)
        {
            var response = await _client.DeleteAsync($"correspondence/api/v1/correspondence/{correspondenceId}/purge");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        var attachment = await _client.GetFromJsonAsync<AttachmentOverviewExt>($"correspondence/api/v1/attachment/{correspondenceResponse.AttachmentIds.FirstOrDefault()}", _responseSerializerOptions);
        Assert.Equal(attachment?.Status, AttachmentStatusExt.Purged);
    }

    [Fact]
    public async Task Delete_correspondence_dont_delete_attachment_with_multiple_correspondences()
    {
        var attachment = await InitializeAttachment();
        Assert.NotNull(attachment);
        var correspondence1 = InitializeCorrespondenceFactory.BasicCorrespondences();
        correspondence1.ExistingAttachments = new List<Guid> { attachment.AttachmentId };
        var correspondence2 = InitializeCorrespondenceFactory.BasicCorrespondences();
        correspondence2.ExistingAttachments = new List<Guid> { attachment.AttachmentId };

        var initializeCorrespondenceResponse1 = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence1, _responseSerializerOptions);
        var response1 = await initializeCorrespondenceResponse1.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();
        initializeCorrespondenceResponse1.EnsureSuccessStatusCode();
        Assert.NotNull(response1);

        var initializeCorrespondenceResponse2 = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence2, _responseSerializerOptions);
        var response2 = await initializeCorrespondenceResponse2.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>();
        initializeCorrespondenceResponse2.EnsureSuccessStatusCode();
        Assert.NotNull(response2);

        var deleteResponse = await _client.DeleteAsync($"correspondence/api/v1/correspondence/{response1.CorrespondenceIds.FirstOrDefault()}/purge");
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
    private MultipartFormDataContent CorrespondenceToFormData(BaseCorrespondenceExt correspondence)
    {
        var formData = new MultipartFormDataContent(){
            { new StringContent(correspondence.ResourceId), "correspondence.resourceId" },
            { new StringContent(correspondence.Sender), "correspondence.sender" },
            { new StringContent(correspondence.SendersReference), "correspondence.sendersReference" },
            { new StringContent(correspondence.VisibleFrom.ToString()), "correspondence.visibleFrom" },
            { new StringContent(correspondence.AllowSystemDeleteAfter.ToString()), "correspondence.AllowSystemDeleteAfter" },
            { new StringContent(correspondence.Content.MessageTitle), "correspondence.content.MessageTitle" },
            { new StringContent(correspondence.Content.MessageSummary), "correspondence.content.MessageSummary" },
            { new StringContent(correspondence.Content.MessageBody), "correspondence.content.MessageBody" },
            { new StringContent(correspondence.Content.Language), "correspondence.content.Language" },
            { new StringContent((correspondence.IsReservable ?? false).ToString()), "correspondence.isReservable" }
        };

        correspondence.Content.Attachments.Select((attachment, index) => new[]
        {
            new { Key = $"correspondence.content.Attachments[{index}].DataLocationType", Value = attachment.DataLocationType.ToString() },
            new { Key = $"correspondence.content.Attachments[{index}].DataType", Value = attachment.DataType },
            new { Key = $"correspondence.content.Attachments[{index}].Name", Value = attachment.Name },
            new { Key = $"correspondence.content.Attachments[{index}].FileName", Value = attachment.FileName ?? "" },
            new { Key = $"correspondence.content.Attachments[{index}].RestrictionName", Value = attachment.RestrictionName },
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

        correspondence.Notifications.Select((notification, index) => new[]
        {
            new { Key = $"correspondence.Notifications[{index}].NotificationTemplate", Value = notification.NotificationTemplate },
            new { Key = $"correspondence.Notifications[{index}].CustomTextToken", Value = notification.CustomTextToken ?? ""},
            new { Key = $"correspondence.Notifications[{index}].SendersReference", Value = notification.SendersReference ?? "" },
            new { Key = $"correspondence.Notifications[{index}].RequestedSendTime", Value = notification.RequestedSendTime.ToString() }
        }).SelectMany(x => x).ToList()
        .ForEach(item => formData.Add(new StringContent(item.Value), item.Key));

        correspondence.PropertyList.ToList()
        .ForEach((item) => formData.Add(new StringContent(item.Value), "correspondence.propertyLists." + item.Key));
        return formData;
    }
}