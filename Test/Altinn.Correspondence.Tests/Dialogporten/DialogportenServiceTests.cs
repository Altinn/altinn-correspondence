using System.Net;
using System.Text;
using System.Text.Json;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Integrations.Dialogporten;
using Altinn.Correspondence.Integrations.Dialogporten.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Altinn.Correspondence.Tests.Factories;

namespace Altinn.Correspondence.Tests.Dialogporten;

public class DialogportenServiceTests
{
    private static (DialogportenService service, Func<string> getLastRequestBody) CreateServiceWithMockedDialogPost(CorrespondenceEntity correspondence)
    {
        var capturedRequestBody = string.Empty;

        var mockHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Post &&
                    m.RequestUri != null &&
                    m.RequestUri.AbsolutePath.EndsWith("/dialogporten/api/v1/serviceowner/dialogs")),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                capturedRequestBody = await req.Content!.ReadAsStringAsync();
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("\"dialog-id\"", Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://dialogporten.example/")
        };

        var mockRepo = new Mock<ICorrespondenceRepository>();
        mockRepo
            .Setup(r => r.GetCorrespondenceById(correspondence.Id, true, true, false, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(correspondence);

        var mockIdem = new Mock<IIdempotencyKeyRepository>();
        mockIdem
            .Setup(i => i.CreateAsync(It.IsAny<IdempotencyKeyEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdempotencyKeyEntity e, CancellationToken _) => e);
        mockIdem
            .Setup(i => i.CreateRangeAsync(It.IsAny<IEnumerable<IdempotencyKeyEntity>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockLogger = new Mock<ILogger<DialogportenService>>();
        var options = Options.Create(new GeneralSettings { CorrespondenceBaseUrl = "https://correspondence.example" });

        var service = new DialogportenService(httpClient, mockRepo.Object, options, mockLogger.Object, mockIdem.Object);
        return (service, () => capturedRequestBody);
    }

    [Fact]
    public async Task CreateCorrespondenceDialog_TruncatesSearchTags_ToMax63AndSucceeds()
    {
        // Arrange
        var correspondenceId = Guid.NewGuid();
        var longValue = new string('A', 100);
        var correspondence = new CorrespondenceEntityBuilder()
            .WithStatus(CorrespondenceStatus.Initialized)
            .WithSendersReference(longValue)
            .WithId(correspondenceId)
            .Build();
        var (service, getBody) = CreateServiceWithMockedDialogPost(correspondence);

        // Act
        var resultId = await service.CreateCorrespondenceDialog(correspondenceId);

        // Assert
        Assert.Equal("dialog-id", resultId);
        var capturedRequestBody = getBody();
        Assert.False(string.IsNullOrWhiteSpace(capturedRequestBody));

        var deserialized = JsonSerializer.Deserialize<CreateDialogRequest>(capturedRequestBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized!.SearchTags);
        Assert.NotEmpty(deserialized.SearchTags);
        Assert.All(deserialized.SearchTags, t => Assert.True(t.Value.Length <= 63));

        var expectedTruncated = longValue.Substring(0, 63);
        Assert.Contains(deserialized.SearchTags, t => t.Value == expectedTruncated);
    }
}


