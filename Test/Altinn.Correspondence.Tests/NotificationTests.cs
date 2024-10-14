using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.API.Models;
using System.Net.Http.Json;
using System.Text.Json;
using Altinn.Correspondence.Application.Configuration;
using Altinn.Correspondence.Application.CheckNotification;

namespace Altinn.Correspondence.Tests;

public class NotificationTests : IClassFixture<MaskinportenWebApplicationFactory>
{
    private readonly MaskinportenWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _responseSerializerOptions;

    public NotificationTests(MaskinportenWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.NotificationCheckScope));
        _responseSerializerOptions = new JsonSerializerOptions(new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        });
        _responseSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    }

    [Fact]
    public async Task CheckNotification_For_Non_Existing_Correspondence()
    {
        var correspondenceId = Guid.NewGuid();
        var response = await _client.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/notification/check");
        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content.ReadAsStringAsync();
        var checkNotificationResponse = JsonSerializer.Deserialize<CheckNotificationResponse>(responseContent, _responseSerializerOptions);
        Assert.NotNull(checkNotificationResponse);
        Assert.False(checkNotificationResponse.SendNotification);
    }

    [Fact]
    public async Task CheckNotification_For_Correspondence_With_Unread_Status_Gives_True()
    {
        var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.SenderScope));
        var correspondence = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var initializeCorrespondenceResponse = await client.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
        var responseContent = await initializeCorrespondenceResponse.Content.ReadAsStringAsync();
        var correspondenceId = JsonSerializer.Deserialize<InitializeCorrespondencesResponseExt>(responseContent, _responseSerializerOptions).CorrespondenceIds.First();

        var response = await _client.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/notification/check");
        var content = await response.Content.ReadAsStringAsync();
        var checkNotificationResponse = JsonSerializer.Deserialize<CheckNotificationResponse>(content, _responseSerializerOptions);
        Assert.NotNull(checkNotificationResponse);
        Assert.True(checkNotificationResponse.SendNotification);
    }

    [Fact]
    public async Task CheckNotification_For_Correspondence_With_Read_Status_Gives_False()
    {
        var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.SenderScope));
        var correspondence = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithRequestedPublishTime(DateTime.UtcNow.AddHours(-1))
            .Build();

        var initializeCorrespondenceResponse = await client.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
        var responseContent = await initializeCorrespondenceResponse.Content.ReadAsStringAsync();
        var correspondenceId = JsonSerializer.Deserialize<InitializeCorrespondencesResponseExt>(responseContent, _responseSerializerOptions).CorrespondenceIds.First();

        var recipientClient = factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.RecipientScope));
        var markasread = await recipientClient.PostAsync($"correspondence/api/v1/correspondence/{correspondenceId}/markasread", null);
        markasread.EnsureSuccessStatusCode();

        var response = await _client.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/notification/check");
        var content = await response.Content.ReadAsStringAsync();
        var checkNotificationResponse = JsonSerializer.Deserialize<CheckNotificationResponse>(content, _responseSerializerOptions);
        Assert.NotNull(checkNotificationResponse);
        Assert.False(checkNotificationResponse.SendNotification);
    }

}
