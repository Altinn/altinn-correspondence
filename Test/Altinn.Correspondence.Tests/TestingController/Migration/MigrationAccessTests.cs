using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.GetCorrespondences;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Migration.Base;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace Altinn.Correspondence.Tests.TestingController.Migration;

[Collection(nameof(CustomWebApplicationTestsCollection))]
public class MigrationAccessTests : MigrationTestBase
{
    private readonly HttpClient _recipientClient;

    public MigrationAccessTests(CustomWebApplicationFactory factory) : base(factory)
    {
        _recipientClient = _factory.CreateClientWithAddedClaims(
                ("notSender", "true"),
                ("scope", AuthorizationConstants.RecipientScope)
            );
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

        CorrespondenceMigrationStatusExt? result = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>(_responseSerializerOptions);
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

        CorrespondenceMigrationStatusExt? result = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>(_responseSerializerOptions);
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

        CorrespondenceMigrationStatusExt? result = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>(_responseSerializerOptions);
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

        CorrespondenceMigrationStatusExt? result = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>(_responseSerializerOptions);
        var createdCorrespondenceId = result.CorrespondenceId;

        // Act
        var getCorrespondenceOverviewResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{createdCorrespondenceId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, getCorrespondenceOverviewResponse.StatusCode);

        var retrievedCorrespondence = await getCorrespondenceOverviewResponse.Content.ReadFromJsonAsync<CorrespondenceOverviewExt>(_responseSerializerOptions);

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

        CorrespondenceMigrationStatusExt? result = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>(_responseSerializerOptions);
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

        CorrespondenceMigrationStatusExt? result = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>(_responseSerializerOptions);
        var createdCorrespondenceId = result.CorrespondenceId;

        // Act
        var getCorrespondenceOverviewResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{createdCorrespondenceId}/details");

        // Assert
        Assert.Equal(HttpStatusCode.OK, getCorrespondenceOverviewResponse.StatusCode);

        var retrievedCorrespondence = await getCorrespondenceOverviewResponse.Content.ReadFromJsonAsync<CorrespondenceDetailsExt>(_responseSerializerOptions);

        Assert.Equal(createdCorrespondenceId, retrievedCorrespondence.CorrespondenceId);
    }

    [Fact]
    public async Task FullLifeCycle_IsMigratingTrue__CorrespondenceNotFound()
    {
        // Arrange
        Guid attachmentId = await UploadAttachment();

        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithExistingAttachments(new List<Guid> { attachmentId })
            .Build();

        var initializeCorrespondenceResponse = await _migrationClient.PostAsJsonAsync("correspondence/api/v1/migration/correspondence", migrateCorrespondenceExt);
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());

        CorrespondenceMigrationStatusExt? result = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>(_responseSerializerOptions);
        var createdCorrespondenceId = result.CorrespondenceId;
        var createdAttachmentId = result.AttachmentStatuses.FirstOrDefault(x => x.AttachmentId == attachmentId)?.AttachmentId;

        // Act
        var fetchResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{createdCorrespondenceId}");
        Assert.Equal(HttpStatusCode.NotFound, fetchResponse.StatusCode);

        var downloadAttachmentResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{createdCorrespondenceId}/attachment/{attachmentId}/download");
        var data = await downloadAttachmentResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(HttpStatusCode.NotFound, downloadAttachmentResponse.StatusCode);

        var markAsReadResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/{createdCorrespondenceId}/markasread", null);
        Assert.Equal(HttpStatusCode.NotFound, markAsReadResponse.StatusCode);

        var confirmResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/{createdCorrespondenceId}/confirm", null);
        Assert.Equal(HttpStatusCode.NotFound, confirmResponse.StatusCode);

        var purgeResponse = await _recipientClient.DeleteAsync($"correspondence/api/v1/correspondence/{createdCorrespondenceId}/purge");
        Assert.Equal(HttpStatusCode.NotFound, purgeResponse.StatusCode);
    }

    [Fact]
    public async Task FullLifeCycle_IsMigratingFalse__OK()
    {
        // Arrange
        Guid attachmentId = await UploadAttachment();

        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithExistingAttachments(new List<Guid> { attachmentId })
            .WithIsMigrating(false)
            .Build();

        var initializeCorrespondenceResponse = await _migrationClient.PostAsJsonAsync("correspondence/api/v1/migration/correspondence", migrateCorrespondenceExt);
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());

        CorrespondenceMigrationStatusExt? result = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>(_responseSerializerOptions);
        var createdCorrespondenceId = result.CorrespondenceId;

        // Act
        var fetchResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{createdCorrespondenceId}");
        Assert.Equal(HttpStatusCode.OK, fetchResponse.StatusCode);

        var downloadAttachmentResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{createdCorrespondenceId}/attachment/{attachmentId}/download");
        var data = await downloadAttachmentResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(HttpStatusCode.OK, downloadAttachmentResponse.StatusCode);
        Assert.NotEmpty(data);

        var markAsReadResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/{createdCorrespondenceId}/markasread", null);
        Assert.Equal(HttpStatusCode.OK, markAsReadResponse.StatusCode);

        var confirmResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/{createdCorrespondenceId}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);

        var purgeResponse = await _recipientClient.DeleteAsync($"correspondence/api/v1/correspondence/{createdCorrespondenceId}/purge");
        Assert.Equal(HttpStatusCode.OK, purgeResponse.StatusCode);
    }

    private async Task<Guid> UploadAttachment()
    {
        MigrateInitializeAttachmentExt migrateAttachmentExt = new MigrateAttachmentBuilder().CreateAttachment().Build();

        byte[] file = Encoding.UTF8.GetBytes("Test av fil opplasting");
        using MemoryStream memoryStream = new(file);
        using StreamContent content = new(memoryStream);
        string command = GetAttachmentCommand(migrateAttachmentExt);
        var uploadResponse = await _migrationClient.PostAsync(command, content);
        Assert.True(uploadResponse.IsSuccessStatusCode, uploadResponse.ReasonPhrase + ":" + await uploadResponse.Content.ReadAsStringAsync());
        string responseContent = await uploadResponse.Content.ReadAsStringAsync();
        Guid attachmentId = Guid.Parse(responseContent.Trim('"'));
        return attachmentId;
    }
}
