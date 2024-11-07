using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.Configuration;
using Altinn.Correspondence.Application.GetCorrespondenceHistory;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Helpers;

namespace Altinn.Correspondence.Tests;
public class LegacyControllerTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly HttpClient _legacyClient;
    private readonly HttpClient _senderClient;

    public LegacyControllerTests()
    {
        _factory = new CustomWebApplicationFactory();
        _serializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        _serializerOptions.Converters.Add(new JsonStringEnumConverter());

        _senderClient = _factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.SenderScope));
        _legacyClient = _factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.LegacyScope), ("urn:altinn:partyid", "50167512"));
    }

    [Fact]
    public async Task LegacyGetCorrespondenceOverview_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
        // Act
        var response = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/overview");
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LegacyGetCorrespondenceOverview_InvalidPartyId_ReturnsUnauthorized()
    {
        // Arrange
        var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
        var client = _factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.LegacyScope), ("urn:altinn:partyid", "123"));
        // Act
        var response = await client.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/overview");
        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    
    [Fact]
    public async Task LegacyGetCorrespondenceOverview_CorrespondenceNotPublished_Returns404()
    {
        // Arrange
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(1))
            .Build();
        var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
        // Act
        var response = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/overview");
        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task LegacyGetCorrespondenceOverview_ShouldAddFetchedToStatusHistory()
    {
        // Arrange
        var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);

        // Act
        var overviewResponse = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/overview");
        Assert.Equal(HttpStatusCode.OK, overviewResponse.StatusCode);
        var overviewContent = await overviewResponse.Content.ReadFromJsonAsync<LegacyCorrespondenceOverviewExt>(_serializerOptions);
        Assert.NotNull(overviewContent);
        Assert.NotEqual(CorrespondenceStatusExt.Fetched, overviewContent.Status);
        

        // Assert
        var historyResponse = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/history");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        var historyData = await historyResponse.Content.ReadFromJsonAsync<LegacyGetCorrespondenceHistoryResponse>();
        Assert.NotNull(historyData);
        Assert.Contains(historyData.History, status => status.Status.Contains(CorrespondenceStatus.Fetched.ToString()));
    }

    [Fact]
    public async Task LegacyGetCorrespondenceHistory_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
        // Act
        var response = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/history");
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LegacyGetCorrespondenceHistory_InvalidPartyId_ReturnsUnauthorized()
    {
        // Arrange
        var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
        var client = _factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.LegacyScope), ("urn:altinn:partyid", "123"));
        // Act
        var response = await client.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/history");
        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LegacyGetCorrespondenceHistory_CorrespondenceNotPublished_Returns404()
    {
        // Arrange
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(1))
            .Build();
        var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
        // Act
        var response = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/history");
        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCorrespondenceStatus_ReturnOk()
    {
        // Arrange
        var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
        Assert.Equal(CorrespondenceStatus.Published, correspondence.Status);

        // Act
        var archiveResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/archive", null);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateCorrespondenceStatus_CorrespondenceNotExists_Return404()
    {
        // Arrange
        Guid randomCorrespondenceId = Guid.NewGuid();

        // Act and Assert
        var confirmResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{randomCorrespondenceId}/confirm", null);
        Assert.Equal(HttpStatusCode.NotFound, confirmResponse.StatusCode);

        // Act and Assert
        var archiveResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{randomCorrespondenceId}/archive", null);
        Assert.Equal(HttpStatusCode.NotFound, archiveResponse.StatusCode);

        // TODO: Missing markasread implementation
        // var readResponse = await _client.PostAsync($"correspondence/api/v1/legacy/correspondence/{randomCorrespondenceId}/markasread", null);
        // Assert.Equal(HttpStatusCode.NotFound, readResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateCorrespondenceStatus_CorrespondenceNotPublished_Return404()
    {
        // Arrange
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(1))
            .Build();
        var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
        Assert.Equal(CorrespondenceStatus.ReadyForPublish, correspondence.Status);

        // Act
        var archiveResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/archive", null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, archiveResponse.StatusCode);
    }

    
    // TODO: Missing markasread implementation
    /*
    [Fact]
    public async Task UpdateCorrespondenceStatus_MarkAsRead_WithoutFetched_ReturnsBadRequest()
    {
        //  Arrange
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .Build();
        var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
        var correspondenceId = correspondence.CorrespondenceId;

        //  Act
        var readResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondenceId}/markasread", null);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, readResponse.StatusCode);
    }
    */
    
    [Fact]
    public async Task UpdateCorrespondenceStatus_ToConfirmed_WithoutFetched_ReturnsBadRequest()
    {
        //  Arrange
        var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);

        //  Act
        var confirmResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/confirm", null);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, confirmResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateCorrespondenceStatus_ToConfirmed_WhenCorrespondenceIsFetched_GivesOk()
    {
        //  Arrange
        var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);

        //  Act
        var fetchResponse = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/overview");
        Assert.Equal(HttpStatusCode.OK, fetchResponse.StatusCode);
        var confirmResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/confirm", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateCorrespondenceStatus_ToArchived_WithoutConfirmation_WhenConfirmationNeeded_ReturnsBadRequest()
    {
        //  Arrange
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithDueDateTime(DateTimeOffset.UtcNow.AddDays(1))
            .WithConfirmationNeeded(true)
            .Build();
        var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);

        //  Act
        var archiveResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/archive", null);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, archiveResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateCorrespondenceStatus_ToArchived_WithConfirmation_WhenConfirmationNeeded_GivesOk()
    {
        //  Arrange
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithDueDateTime(DateTimeOffset.UtcNow.AddDays(1))
            .WithConfirmationNeeded(true)
            .Build();
        var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);

        //  Act
        var fetchResponse = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/overview"); // Fetch in order to be able to Confirm
        Assert.Equal(HttpStatusCode.OK, fetchResponse.StatusCode);
        var confirmResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/confirm", null); // Update to Confirmed in order to be able to Archive
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        var archiveResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/archive", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);
    }
}