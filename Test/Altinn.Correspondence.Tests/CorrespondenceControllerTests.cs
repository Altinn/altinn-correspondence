using Altinn.Correspondece.Tests.Factories;
using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.GetCorrespondences;
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
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondence());
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task InitializeCorrespondence_No_Message_Body_fails()
    {
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondenceWithNoMessageBody());
        Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task GetCorrespondences()
    {
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondence());
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());

        var initializeCorrespondenceResponse2 = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondence());
        Assert.True(initializeCorrespondenceResponse2.IsSuccessStatusCode, await initializeCorrespondenceResponse2.Content.ReadAsStringAsync());

        var correspondenceList = await _client.GetFromJsonAsync<GetCorrespondencesResponse>("correspondence/api/v1/correspondence?offset=0&limit=10&status=0");
        Assert.True(correspondenceList?.Pagination.TotalItems > 0);
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

        var markAsUndreadResponde = await _client.PostAsync($"correspondence/api/v1/correspondence/{response.CorrespondenceId}/markasunread", null);
        markAsUndreadResponde.EnsureSuccessStatusCode();
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
}