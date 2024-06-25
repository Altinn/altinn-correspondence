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

        var readResponse = await _client.PostAsync($"correspondence/api/v1/correspondence/{correspondence?.CorrespondenceId}/markasread", null);
        Assert.Equal(HttpStatusCode.BadRequest, readResponse.StatusCode);

        var confirmResponse = await _client.PostAsync($"correspondence/api/v1/correspondence/{correspondence?.CorrespondenceId}/confirm", null);
        Assert.Equal(HttpStatusCode.BadRequest, confirmResponse.StatusCode);
    }

    [Fact]
    public async Task ReceiverMarkActions_CorrespondencePublished_ReturnOk()
    {
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondenceAlreadyVisibleWithNoAttachment());
        var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondenceResponseExt>();
        var overview = await _client.GetFromJsonAsync<CorrespondenceOverviewExt>($"correspondence/api/v1/correspondence/{correspondence?.CorrespondenceId}", _responseSerializerOptions);
        Assert.True(overview?.Status == CorrespondenceStatusExt.Published);

        var readResponse = await _client.PostAsync($"correspondence/api/v1/correspondence/{correspondence?.CorrespondenceId}/markasread", null);
        Assert.True(readResponse.IsSuccessStatusCode, await readResponse.Content.ReadAsStringAsync());

        var confirmResponse = await _client.PostAsync($"correspondence/api/v1/correspondence/{correspondence?.CorrespondenceId}/confirm", null);
        Assert.True(confirmResponse.IsSuccessStatusCode, await confirmResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Correspondence_with_dataLocationUrl_Reuses_Attachment()
    {
        var attachment = InitializeAttachmentFactory.BasicAttachment();
        var initializeResponse = await _client.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
        initializeResponse.EnsureSuccessStatusCode();
        var attachmentId = await initializeResponse.Content.ReadAsStringAsync();
        var uploadedAttachment = await (await UploadAttachment(attachmentId)).Content.ReadFromJsonAsync<AttachmentOverviewExt>(_responseSerializerOptions);
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/correspondence", InitializeCorrespondenceFactory.BasicCorrespondenceWithFileAttachment(uploadedAttachment.DataLocationUrl), _responseSerializerOptions);
        var response = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondenceResponseExt>();
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
        Assert.Equal(attachmentId, response?.AttachmentIds?.FirstOrDefault().ToString());
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
}