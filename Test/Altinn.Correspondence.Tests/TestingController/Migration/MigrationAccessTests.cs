using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Altinn.Correspondence.Core.Models.Entities;
using Microsoft.Extensions.DependencyInjection;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Application.GetCorrespondences;
using Npgsql.Internal;
using System.Net;

namespace Altinn.Correspondence.Tests.TestingController.Migration;

[Collection(nameof(CustomWebApplicationTestsCollection))]
public class MigrationAccessTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _migrationClient, _recipientClient, _legacyClient;
    private readonly JsonSerializerOptions _serializerOptions;

    public readonly string _partyIdClaim = "urn:altinn:partyid";
    public readonly int _digdirPartyId = 50952483;

    public MigrationAccessTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _migrationClient = _factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.MigrateScope));
        _recipientClient = _factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.RecipientScope));
        _legacyClient = _factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.RecipientScope));
        _serializerOptions = new JsonSerializerOptions(new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        });
        _serializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());

        _legacyClient = _factory.CreateClientWithAddedClaims(
                ("scope", AuthorizationConstants.LegacyScope),
                (_partyIdClaim, _digdirPartyId.ToString())
            );
        _recipientClient = _factory.CreateClientWithAddedClaims(
                ("notSender", "true"),
                ("scope", AuthorizationConstants.RecipientScope)
            );
    }

    [Fact]
    public async Task LegacyGetCorrespondences_IsMigratingTrue_WithFilterEnabled__NoCorrespondenceFound()
    {
        // Arrange
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .Build();

        // Act
        var initializeCorrespondenceResponse = await _migrationClient.PostAsJsonAsync("correspondence/api/v1/migration/correspondence", migrateCorrespondenceExt);
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());

        CorrespondenceMigrationStatusExt? result = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>(_serializerOptions);
        var createdCorrespondenceId = result.CorrespondenceId;

        var correspondenceList = await _legacyClient.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", GetBasicLegacyGetCorrespondenceRequestExt());
        Assert.True(correspondenceList.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());

        var response = await correspondenceList.Content.ReadFromJsonAsync<LegacyGetCorrespondencesResponse>(_serializerOptions);
        Assert.False(response?.Items.Any(x => x.CorrespondenceId == createdCorrespondenceId));
    }

    [Fact]
    public async Task LegacyGetCorrespondences_IsMigratingFalse_WithFilterEnabled__NoCorrespondenceFound()
    {
        // Arrange
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithIsMigrating(false)
            .Build();

        // Act
        var initializeCorrespondenceResponse = await _migrationClient.PostAsJsonAsync("correspondence/api/v1/migration/correspondence", migrateCorrespondenceExt);
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());

        CorrespondenceMigrationStatusExt? result = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>(_serializerOptions);
        var createdCorrespondenceId = result.CorrespondenceId;

        var correspondenceList = await _legacyClient.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", GetBasicLegacyGetCorrespondenceRequestExt());
        Assert.True(correspondenceList.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());

        var response = await correspondenceList.Content.ReadFromJsonAsync<LegacyGetCorrespondencesResponse>(_serializerOptions);
        Assert.False(response?.Items.Any(x => x.CorrespondenceId == createdCorrespondenceId));
    }

    [Fact]
    public async Task LegacyGetCorrespondences_IsMigratingTrue_WithFilterDisabled__CorrespondenceFound()
    {
        // Arrange
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .Build();

        // Act
        var initializeCorrespondenceResponse = await _migrationClient.PostAsJsonAsync("correspondence/api/v1/migration/correspondence", migrateCorrespondenceExt);
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());

        CorrespondenceMigrationStatusExt? result = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>(_serializerOptions);
        var createdCorrespondenceId = result.CorrespondenceId;

        var request = GetBasicLegacyGetCorrespondenceRequestExt();
        request.FilterMigrated = false;

        var correspondenceList = await _legacyClient.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", request);
        Assert.True(correspondenceList.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());

        var response = await correspondenceList.Content.ReadFromJsonAsync<LegacyGetCorrespondencesResponse>(_serializerOptions);
        Assert.True(response?.Items.Any(x => x.CorrespondenceId == createdCorrespondenceId));
    }

    [Fact]
    public async Task LegacyGetCorrespondences_IsMigratingFalse_WithFilterDisabled__CorrespondenceFound()
    {
        // Arrange
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithIsMigrating(false)
            .Build();

        // Act
        var initializeCorrespondenceResponse = await _migrationClient.PostAsJsonAsync("correspondence/api/v1/migration/correspondence", migrateCorrespondenceExt);
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());

        CorrespondenceMigrationStatusExt? result = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>(_serializerOptions);
        var createdCorrespondenceId = result.CorrespondenceId;

        var request = GetBasicLegacyGetCorrespondenceRequestExt();
        request.FilterMigrated = false;

        var correspondenceList = await _legacyClient.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", request);
        Assert.True(correspondenceList.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());

        var response = await correspondenceList.Content.ReadFromJsonAsync<LegacyGetCorrespondencesResponse>(_serializerOptions);
        Assert.True(response?.Items.Any(x => x.CorrespondenceId == createdCorrespondenceId));
    }

    [Fact]
    public async Task GetCorrespondences_IsMigratingFalse__CorrespondenceFound()
    {
        // Arrange
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithIsMigrating(false)
            .Build();

        // Act
        var initializeCorrespondenceResponse = await _migrationClient.PostAsJsonAsync("correspondence/api/v1/migration/correspondence", migrateCorrespondenceExt);
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());

        CorrespondenceMigrationStatusExt? result = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>(_serializerOptions);
        var createdCorrespondenceId = result.CorrespondenceId;

        string resourceId = migrateCorrespondenceExt.CorrespondenceData.Correspondence.ResourceId.ToString();
        string status = CorrespondenceStatusExt.Published.ToString();

        var correspondenceList = await _recipientClient.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={resourceId}&status={status}&role={"recipient"}");
        Assert.True(correspondenceList.Ids.Any(x => x == createdCorrespondenceId));
    }

    [Fact]
    public async Task GetCorrespondences_IsMigratingTrue__NoCorrespondenceFound()
    {
        // Arrange
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .Build();

        // Act
        var initializeCorrespondenceResponse = await _migrationClient.PostAsJsonAsync("correspondence/api/v1/migration/correspondence", migrateCorrespondenceExt);
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());

        CorrespondenceMigrationStatusExt? result = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>(_serializerOptions);
        var createdCorrespondenceId = result.CorrespondenceId;

        string resourceId = migrateCorrespondenceExt.CorrespondenceData.Correspondence.ResourceId.ToString();
        string status = CorrespondenceStatusExt.Published.ToString();

        var correspondenceList = await _recipientClient.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={resourceId}&status={status}&role={"recipient"}");
        Assert.False(correspondenceList.Ids.Any(x => x == createdCorrespondenceId));
    }

    [Fact]
    public async Task GetCorrespondenceOverview_IsMigratingTrue__CorrespondenceNotFound()
    {
        // Arrange
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .Build();

        var initializeCorrespondenceResponse = await _migrationClient.PostAsJsonAsync("correspondence/api/v1/migration/correspondence", migrateCorrespondenceExt);
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());

        CorrespondenceMigrationStatusExt? result = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>(_serializerOptions);
        var createdCorrespondenceId = result.CorrespondenceId;

        // Act
        var getCorrespondenceOverviewResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{createdCorrespondenceId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, getCorrespondenceOverviewResponse.StatusCode);
    }

    [Fact]
    public async Task GetCorrespondenceOverview_IsMigratingFalse__CorrespondenceFound()
    {
        // Arrange
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithIsMigrating(false)
            .Build();

        var initializeCorrespondenceResponse = await _migrationClient.PostAsJsonAsync("correspondence/api/v1/migration/correspondence", migrateCorrespondenceExt);
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());

        CorrespondenceMigrationStatusExt? result = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>(_serializerOptions);
        var createdCorrespondenceId = result.CorrespondenceId;

        // Act
        var getCorrespondenceOverviewResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{createdCorrespondenceId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, getCorrespondenceOverviewResponse.StatusCode);

        var retrievedCorrespondence = await getCorrespondenceOverviewResponse.Content.ReadFromJsonAsync<CorrespondenceOverviewExt>(_serializerOptions);

        Assert.Equal(createdCorrespondenceId, retrievedCorrespondence.CorrespondenceId);
    }

    [Fact]
    public async Task GetCorrespondenceDetails_IsMigratingTrue__CorrespondenceNotFound()
    {
        // Arrange
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .Build();

        var initializeCorrespondenceResponse = await _migrationClient.PostAsJsonAsync("correspondence/api/v1/migration/correspondence", migrateCorrespondenceExt);
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());

        CorrespondenceMigrationStatusExt? result = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>(_serializerOptions);
        var createdCorrespondenceId = result.CorrespondenceId;

        // Act
        var getCorrespondenceOverviewResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{createdCorrespondenceId}/details");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, getCorrespondenceOverviewResponse.StatusCode);
    }

    [Fact]
    public async Task GetCorrespondenceDetails_IsMigratingFalse__CorrespondenceFound()
    {
        // Arrange
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithIsMigrating(false)
            .Build();

        var initializeCorrespondenceResponse = await _migrationClient.PostAsJsonAsync("correspondence/api/v1/migration/correspondence", migrateCorrespondenceExt);
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());

        CorrespondenceMigrationStatusExt? result = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>(_serializerOptions);
        var createdCorrespondenceId = result.CorrespondenceId;

        // Act
        var getCorrespondenceOverviewResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{createdCorrespondenceId}/details");

        // Assert
        Assert.Equal(HttpStatusCode.OK, getCorrespondenceOverviewResponse.StatusCode);

        var retrievedCorrespondence = await getCorrespondenceOverviewResponse.Content.ReadFromJsonAsync<CorrespondenceDetailsExt>(_serializerOptions);

        Assert.Equal(createdCorrespondenceId, retrievedCorrespondence.CorrespondenceId);
    }

    public LegacyGetCorrespondencesRequestExt GetBasicLegacyGetCorrespondenceRequestExt()
    {
        return new LegacyGetCorrespondencesRequestExt
        {
            InstanceOwnerPartyIdList = new int[] { },
            IncludeActive = true,
            IncludeArchived = true,
            IncludeDeleted = true,
            From = DateTimeOffset.UtcNow.AddDays(-5),
            To = DateTimeOffset.UtcNow.AddDays(5),
        };
    }
}
