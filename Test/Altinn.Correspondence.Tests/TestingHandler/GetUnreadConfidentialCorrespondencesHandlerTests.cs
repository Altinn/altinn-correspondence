using Altinn.Correspondence.Application;
using Altinn.Correspondence.Application.GetUnreadConfidentialCorrespondences;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Tests.Factories;
using Microsoft.Extensions.Hosting;
using Moq;
using System.Security.Claims;

namespace Altinn.Correspondence.Tests.TestingHandler;

public class GetUnreadConfidentialCorrespondencesHandlerTests
{
    private readonly Mock<ICorrespondenceRepository> _correspondenceRepositoryMock;
    private readonly Mock<IAltinnAuthorizationService> _altinnAuthorizationServiceMock;
    private readonly Mock<IAltinnRegisterService> _altinnRegisterServiceMock;
    private readonly Mock<IHostEnvironment> _hostEnvironmentMock;
    private readonly GetUnreadConfidentialCorrespondencesHandler _handler;

    public GetUnreadConfidentialCorrespondencesHandlerTests()
    {
        _correspondenceRepositoryMock = new Mock<ICorrespondenceRepository>();
        _altinnAuthorizationServiceMock = new Mock<IAltinnAuthorizationService>();
        _altinnRegisterServiceMock = new Mock<IAltinnRegisterService>();
        _hostEnvironmentMock = new Mock<IHostEnvironment>();
        _hostEnvironmentMock.Setup(x => x.EnvironmentName).Returns("Development");

        _handler = new GetUnreadConfidentialCorrespondencesHandler(
            _correspondenceRepositoryMock.Object,
            _altinnAuthorizationServiceMock.Object,
            _altinnRegisterServiceMock.Object,
            _hostEnvironmentMock.Object);
    }

    private static ClaimsPrincipal CreateOrgUser(string orgNumber = "991825827")
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("urn:altinn:orgNumber", orgNumber)
        ], "TestAuthType"));
    }

    private static CorrespondenceEntity CreateCorrespondenceForListing(string sender, DateTimeOffset published, string resourceId = "test-resource")
    {
        var entity = new CorrespondenceEntityBuilder()
            .WithRecipient("urn:altinn:organization:identifier-no:991825827")
            .WithResourceId(resourceId)
            .Build();
        entity.Sender = sender;
        entity.Published = published;
        return entity;
    }

    [Fact]
    public async Task Process_NoCallerOrg_ReturnsCouldNotDetermineCallerError()
    {
        // Arrange
        var user = new ClaimsPrincipal();

        // Act
        var result = await _handler.Process(user, CancellationToken.None);

        // Assert
        Assert.True(result.IsT1);
        Assert.Equal(AuthorizationErrors.CouldNotDetermineCaller.ErrorCode, result.AsT1.ErrorCode);
        _altinnAuthorizationServiceMock.Verify(
            x => x.CheckAccessAsAny(It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Process_NoAccess_ReturnsNoAccessToResourceError()
    {
        // Arrange
        var user = CreateOrgUser();
        _altinnAuthorizationServiceMock
            .Setup(x => x.CheckAccessAsAny(It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Process(user, CancellationToken.None);

        // Assert
        Assert.True(result.IsT1);
        Assert.Equal(AuthorizationErrors.NoAccessToResource.ErrorCode, result.AsT1.ErrorCode);
        _correspondenceRepositoryMock.Verify(
            x => x.GetUnopenedConfidentialCorrespondencesForParty(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Process_WithCorrespondences_ReturnsFormattedTextContainingSenderAndResourceId()
    {
        // Arrange
        var user = CreateOrgUser();
        var published = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var correspondence = CreateCorrespondenceForListing(
            "urn:altinn:organization:identifier-no:310300942",
            published,
            "some-resource-id");

        _altinnAuthorizationServiceMock
            .Setup(x => x.CheckAccessAsAny(It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _correspondenceRepositoryMock
            .Setup(x => x.GetUnopenedConfidentialCorrespondencesForParty(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CorrespondenceEntity> { correspondence });

        // Act
        var result = await _handler.Process(user, CancellationToken.None);

        // Assert
        Assert.True(result.IsT0);
        var text = result.AsT0.Text;
        Assert.Contains("310300942", text);
        Assert.Contains("some-resource-id", text);
        Assert.Contains("15.01.2026", text);
        Assert.Contains("1.", text);
    }

    [Fact]
    public async Task Process_WithMultipleCorrespondences_OrdersResultsByPublishDate()
    {
        // Arrange
        var user = CreateOrgUser();
        var older = CreateCorrespondenceForListing("urn:altinn:organization:identifier-no:111111111", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), "older-resource");
        var newer = CreateCorrespondenceForListing("urn:altinn:organization:identifier-no:222222222", new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), "newer-resource");

        _altinnAuthorizationServiceMock
            .Setup(x => x.CheckAccessAsAny(It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _correspondenceRepositoryMock
            .Setup(x => x.GetUnopenedConfidentialCorrespondencesForParty(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CorrespondenceEntity> { newer, older });

        // Act
        var result = await _handler.Process(user, CancellationToken.None);

        // Assert
        Assert.True(result.IsT0);
        var text = result.AsT0.Text;
        var olderIndex = text.IndexOf("older-resource", StringComparison.Ordinal);
        var newerIndex = text.IndexOf("newer-resource", StringComparison.Ordinal);
        Assert.True(olderIndex < newerIndex, "Older correspondence should appear before newer in the formatted text");
    }

    [Fact]
    public async Task Process_NonProductionEnvironment_UsesOneMinuteAsMinAge()
    {
        // Arrange
        var user = CreateOrgUser();
        TimeSpan? capturedMinAge = null;
        _altinnAuthorizationServiceMock
            .Setup(x => x.CheckAccessAsAny(It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _correspondenceRepositoryMock
            .Setup(x => x.GetUnopenedConfidentialCorrespondencesForParty(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback<string, TimeSpan, CancellationToken>((_, minAge, _) => capturedMinAge = minAge)
            .ReturnsAsync(new List<CorrespondenceEntity>());

        // Act
        await _handler.Process(user, CancellationToken.None);

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(1), capturedMinAge);
    }

    [Fact]
    public async Task Process_ConfidentialCorrespondencesNotFound_ReturnsNotFound()
    {
        // Arrange
        var user = CreateOrgUser();
        _altinnAuthorizationServiceMock
            .Setup(x => x.CheckAccessAsAny(It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _correspondenceRepositoryMock
            .Setup(x => x.GetUnopenedConfidentialCorrespondencesForParty(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CorrespondenceEntity>());

        // Act
        var result = await _handler.Process(user, CancellationToken.None);

        // Assert
        Assert.True(result.IsT1);
        Assert.Equal(CorrespondenceErrors.UnreadConfidentialCorrespondencesNotFound.ErrorCode, result.AsT1.ErrorCode);
    }
}
