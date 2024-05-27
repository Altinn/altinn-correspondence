using Altinn.Correspondece.Tests.Factories;
using Altinn.Correspondence.Application.GetCorrespondencesResponse;

namespace Altinn.Correspondence.Tests;

public class CorrespondenceControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CorrespondenceControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClientInternal();
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

        var getCorrespondencesResponse = await _client.GetAsync("correspondence/api/v1/correspondence?offset=0&limit=10");
        Assert.True(getCorrespondencesResponse.IsSuccessStatusCode, await getCorrespondencesResponse.Content.ReadAsStringAsync());
        var data = await getCorrespondencesResponse.Content.ReadAsAsync<GetCorrespondencesCommandResponse>();
        Assert.True(data.Pagination.TotalItems > 0);
    }
}