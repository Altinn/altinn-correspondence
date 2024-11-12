using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.Configuration;
using Altinn.Correspondence.Application.GetCorrespondenceHistory;
using Altinn.Correspondence.Application.GetCorrespondences;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Altinn.Correspondence.Tests;
public class LegacyControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly HttpClient _legacyClient;
    private readonly HttpClient _senderClient;
    private readonly string _partyIdClaim = "urn:altinn:partyid";
    private readonly int _digdirPartyId = 50167512;

    public LegacyControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _serializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
        _serializerOptions.Converters.Add(new JsonStringEnumConverter());

        _senderClient = _factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.SenderScope));
        _legacyClient = _factory.CreateClientWithAddedClaims(
            ("scope", AuthorizationConstants.LegacyScope),
            (_partyIdClaim, _digdirPartyId.ToString()));
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
    public async Task LegacyGetCorrespondenceOverview_InvalidPartyId_ReturnsBadRequest()
    {
        // Arrange
        var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
        var failClient = _factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.LegacyScope), (_partyIdClaim, "123abc"));

        // Act
        var response = await failClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/overview");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
    public async Task LegacyGetCorrespondenceHistory_InvalidPartyId_ReturnsBadRequest()
    {
        // Arrange
        var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
        var failClient = _factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.LegacyScope), (_partyIdClaim, "123abc"));

        // Act
        var response = await failClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/history");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LegacyGetCorrespondenceHistory_WithCorrespondenceActions_IncludesStatuses()
    {
        // Arrange
        var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
        await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/overview");
        await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/confirm", null);
        await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/archive", null);

        // Act
        var response = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/history");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Assert
        var content = await response.Content.ReadFromJsonAsync<LegacyGetCorrespondenceHistoryResponse>(_serializerOptions);
        Assert.NotNull(content);
        Assert.Equal(content.NeedsConfirm, payload.Correspondence.IsConfirmationNeeded);
        Assert.All(content.History, status => Assert.True(status.User.AuthenticationLevel > 0));
        Assert.Contains(content.History, status => status.User.PartyId == _digdirPartyId.ToString());
        Assert.Contains(content.History, status => status.Status.Contains(CorrespondenceStatus.Published.ToString()));
        Assert.Contains(content.History, status => status.Status.Contains(CorrespondenceStatus.Fetched.ToString()));
        Assert.Contains(content.History, status => status.Status.Contains(CorrespondenceStatus.Confirmed.ToString()));
        Assert.Contains(content.History, status => status.Status.Contains(CorrespondenceStatus.Archived.ToString()));
    }

    [Fact]
    public async Task LegacyGetCorrespondenceHistory_InvalidPartyId_ReturnsUnauthorized()
    {
        // Arrange
        var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
        var factory = new UnitWebApplicationFactory((IServiceCollection services) =>
        {
            var mockRegisterService = new Mock<IAltinnRegisterService>();
            mockRegisterService
                .Setup(service => service.LookUpPartyByPartyId(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Party)null);
            services.AddSingleton(mockRegisterService.Object);
        });
        var failClient = factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.LegacyScope), (_partyIdClaim, "123"));

        // Act
        var response = await failClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/history");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCorrespondenceStatus_ReturnOk()
    {
        // Arrange
        var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
        Assert.Equal(CorrespondenceStatusExt.Published, correspondence.Status);

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

        // Act and Assert
        var readResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{randomCorrespondenceId}/markasread", null);
        Assert.Equal(HttpStatusCode.NotFound, readResponse.StatusCode);
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
        Assert.Equal(CorrespondenceStatusExt.ReadyForPublish, correspondence.Status);

        // Act
        var archiveResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/archive", null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, archiveResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateCorrespondenceStatus_CorrespondencePublished_ReturnOk()
    {
        // Arrange
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .Build();
        var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
        Assert.Equal(CorrespondenceStatusExt.Published, correspondence.Status);
        var correspondenceId = correspondence.CorrespondenceId;

        // Act
        var archiveResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondenceId}/archive", null);
        // Assert
        Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);
    }

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
        Assert.Equal(CorrespondenceStatusExt.Published, correspondence.Status);

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
        Assert.Equal(CorrespondenceStatusExt.Published, correspondence.Status);

        //  Act
        var fetchResponse = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/overview"); // Fetch in order to be able to Confirm
        Assert.Equal(HttpStatusCode.OK, fetchResponse.StatusCode);
        var confirmResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/confirm", null); // Update to Confirmed in order to be able to Archive
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        var archiveResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/archive", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);
    }

    [Fact]
    public async Task GetCorrespondences()
    {
        var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());

        var correspondenceList = await _legacyClient.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", GetBasicLegacyGetCorrespondenceRequestExt());
        var response = await correspondenceList.Content.ReadFromJsonAsync<LegacyGetCorrespondencesResponse>(_serializerOptions);

        Assert.True(response?.Items.Count > 0);
        Assert.True(response?.Pagination.TotalItems > 0);
    }

    [Fact]
    public async Task GetCorrespondencesFromTokenOnly()
    {
        var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());

        var listPayload = GetBasicLegacyGetCorrespondenceRequestExt();
        listPayload.InstanceOwnerPartyIdList = new int[] { };
        var correspondenceList = await _legacyClient.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", listPayload);
        var response = await correspondenceList.Content.ReadFromJsonAsync<LegacyGetCorrespondencesResponse>(_serializerOptions);
        Assert.Equal(HttpStatusCode.OK, correspondenceList.StatusCode);
        Assert.True(response?.Pagination.TotalItems > 0);
    }
    [Fact]
    public async Task GetCorrespondences_With_Different_statuses()
    {
        var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
        var listPayload = GetBasicLegacyGetCorrespondenceRequestExt();
        listPayload.Status = CorrespondenceStatusExt.Published;

        var correspondenceList = await _legacyClient.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", listPayload);
        var response = await correspondenceList.Content.ReadFromJsonAsync<LegacyGetCorrespondencesResponse>(_serializerOptions);
        Assert.True(response?.Items.Count > 0);
        Assert.True(response?.Pagination.TotalItems > 0);

        await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/confirm", null); // Update to Confirmed in order to be able to Archive
        listPayload.Status = CorrespondenceStatusExt.Confirmed;
        correspondenceList = await _legacyClient.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", listPayload);
        response = await correspondenceList.Content.ReadFromJsonAsync<LegacyGetCorrespondencesResponse>(_serializerOptions);
        Assert.True(response?.Items.Count > 0);
        Assert.True(response?.Pagination.TotalItems > 0);
        await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/archive", null);
        listPayload.Status = CorrespondenceStatusExt.Archived;
        correspondenceList = await _legacyClient.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", listPayload);
        response = await correspondenceList.Content.ReadFromJsonAsync<LegacyGetCorrespondencesResponse>(_serializerOptions);
        Assert.True(response?.Items.Count > 0);
        Assert.True(response?.Pagination.TotalItems > 0);
    }

    [Fact]
    public async Task GetCorrespondences_With_Archived()
    {
        var payload = new CorrespondenceBuilder()
                  .CreateCorrespondence()
                  .WithDueDateTime(DateTimeOffset.UtcNow.AddDays(1))
                  .WithConfirmationNeeded(true)
                  .Build();
        var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
        Assert.Equal(CorrespondenceStatusExt.Published, correspondence.Status);

        //  Act
        var fetchResponse = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/overview"); // Fetch in order to be able to Confirm
        Assert.Equal(HttpStatusCode.OK, fetchResponse.StatusCode);
        var confirmResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/confirm", null); // Update to Confirmed in order to be able to Archive
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        var archiveResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/archive", null);
        Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);
        var listPayload = GetBasicLegacyGetCorrespondenceRequestExt();
        listPayload.IncludeActive = false;
        listPayload.IncludeArchived = true;
        listPayload.IncludeDeleted = false;
        var correspondenceList = await _legacyClient.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", listPayload);
        var response = await correspondenceList.Content.ReadFromJsonAsync<LegacyGetCorrespondencesResponse>(_serializerOptions);
        Assert.True(response?.Items.Count > 0);
        Assert.True(response?.Pagination.TotalItems > 0);
    }

    [Fact]
    public async Task GetCorrespondences_With_Purged()
    {
        var payload = new CorrespondenceBuilder()
                  .CreateCorrespondence()
                  .WithDueDateTime(DateTimeOffset.UtcNow.AddDays(1))
                  .WithConfirmationNeeded(true)
                  .Build();
        var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
        Assert.Equal(CorrespondenceStatusExt.Published, correspondence.Status);

        await _legacyClient.DeleteAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/purge");

        var listPayload = GetBasicLegacyGetCorrespondenceRequestExt();
        listPayload.IncludeActive = false;
        listPayload.IncludeArchived = false;
        listPayload.IncludeDeleted = true;
        var correspondenceList = await _legacyClient.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", listPayload);
        var response = await correspondenceList.Content.ReadFromJsonAsync<LegacyGetCorrespondencesResponse>(_serializerOptions);
        Assert.True(response?.Items.Count > 0);
        Assert.True(response?.Pagination.TotalItems > 0);
    }

    [Fact]
    public async Task LegacyGetCorrespondences_InvalidPartyId_ReturnsUnauthorized()
    {
        var factory = new UnitWebApplicationFactory((IServiceCollection services) =>
        {
            var mockRegisterService = new Mock<IAltinnRegisterService>();
            mockRegisterService
                .Setup(service => service.LookUpPartyByPartyId(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Party)null);
            services.AddSingleton(mockRegisterService.Object);
        });
        var failClient = factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.LegacyScope), (_partyIdClaim, "123"));
        var listPayload = GetBasicLegacyGetCorrespondenceRequestExt();
        var response = await failClient.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", listPayload);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LegacyGetCorrespondences_InvalidDateTimes_GivesBadRequest()
    {
        var listPayload = GetBasicLegacyGetCorrespondenceRequestExt();
        listPayload.From = DateTimeOffset.UtcNow.AddDays(1);
        listPayload.To = DateTimeOffset.UtcNow.AddDays(5);
        var correspondenceList = await _legacyClient.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", listPayload);
        Assert.Equal(HttpStatusCode.BadRequest, correspondenceList.StatusCode);

        listPayload.From = DateTimeOffset.UtcNow;
        listPayload.To = DateTimeOffset.UtcNow.AddDays(-1);
        correspondenceList = await _legacyClient.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", listPayload);
        Assert.Equal(HttpStatusCode.BadRequest, correspondenceList.StatusCode);
    }

    private LegacyGetCorrespondencesRequestExt GetBasicLegacyGetCorrespondenceRequestExt()
    {
        return new LegacyGetCorrespondencesRequestExt
        {
            Offset = 0,
            Limit = 10,
            InstanceOwnerPartyIdList = new int[] { _digdirPartyId },
            IncludeActive = true,
            IncludeArchived = true,
            IncludeDeleted = true,
            From = DateTimeOffset.UtcNow.AddDays(-5),
            To = DateTimeOffset.UtcNow.AddDays(5),
        };
    }

    public async Task Delete_Published_Correspondence_GivesOk()
    {
        // Arrange
        var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);

        // Act
        var deleteResponse = await _legacyClient.DeleteAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/purge");

        // Assert
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        var overview = await _senderClient.GetFromJsonAsync<CorrespondenceOverviewExt>($"correspondence/api/v1/correspondence/{correspondence.CorrespondenceId}", _serializerOptions);
        Assert.Equal(CorrespondenceStatusExt.PurgedByRecipient, overview.Status);
    }

    [Fact]
    public async Task Delete_Correspondence_InvalidParyId_Gives()
    {
        // Arrange
        var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
        var factory = new UnitWebApplicationFactory((IServiceCollection services) =>
        {
            var mockRegisterService = new Mock<IAltinnRegisterService>();
            mockRegisterService
                .Setup(service => service.LookUpPartyByPartyId(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Party)null);
            services.AddSingleton(mockRegisterService.Object);
        });
        var failClient = factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.LegacyScope), (_partyIdClaim, "123"));

        // Act
        var deleteResponse = await failClient.DeleteAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/purge");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_NonExistent_Correspondence_Returns404()
    {
        // Arrange
        Guid randomCorrespondenceId = Guid.NewGuid();

        // Act
        var deleteResponse = await _legacyClient.DeleteAsync($"correspondence/api/v1/legacy/correspondence/{randomCorrespondenceId}/purge");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_ReadyForPublish_Correspondence_Returns404()
    {
        // Arrange
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(1))
            .Build();
        var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);

        // Act
        var deleteResponse = await _legacyClient.DeleteAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/purge");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_Published_Correspondence_WithoutConfirmation_WhenConfirmationNeeded_ReturnsBadRequest()
    {
        // Arrange
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithDueDateTime(DateTimeOffset.UtcNow.AddDays(1))
            .WithConfirmationNeeded(true)
            .Build();
        var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);

        // Act
        var deleteResponse = await _legacyClient.DeleteAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/purge");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_Published_Correspondence_WithConfirmation_WhenConfirmationNeeded_Gives_OK()
    {
        // Arrange
        var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithDueDateTime(DateTimeOffset.UtcNow.AddDays(1))
            .WithConfirmationNeeded(true)
            .Build();
        var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);

        // Act
        var fetchResponse = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/overview"); // Fetch in order to be able to Confirm
        Assert.Equal(HttpStatusCode.OK, fetchResponse.StatusCode);
        var confirmResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        var deleteResponse = await _legacyClient.DeleteAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/purge");

        // Assert
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
    }
}