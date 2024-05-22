using Altinn.Correspondece.Tests.Factories;

namespace Altinn.Correspondence.Tests;

public class AttachmentControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AttachmentControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClientInternal();
    }

    [Fact]
    public async Task InitializeAttachment()
    {
        var initializeAttachmentResponse = await _client.PostAsJsonAsync("correspondence/api/v1/attachment", InitializeAttachmentFactory.BasicAttachment());
        Assert.True(initializeAttachmentResponse.IsSuccessStatusCode, await initializeAttachmentResponse.Content.ReadAsStringAsync());
    }
}