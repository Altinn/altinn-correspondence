using Altinn.Correspondence.Application.ConfirmCorrespondence;
using Altinn.Correspondence.Application.VerifyCorrespondenceConfirmation;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Tests.Factories;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace Altinn.Correspondence.Tests.TestingHandler;

public class ConfirmCorrespondenceHandlerTests
{
    private readonly Mock<IAltinnAuthorizationService> _altinnAuthorizationServiceMock = new();
    private readonly Mock<IAltinnRegisterService> _altinnRegisterServiceMock = new();
    private readonly Mock<ICorrespondenceRepository> _correspondenceRepositoryMock = new();
    private readonly Mock<IBackgroundJobClient> _backgroundJobClientMock = new();
    private readonly Mock<IDialogportenService> _dialogportenServiceMock = new();
    private readonly Mock<ILogger<ConfirmCorrespondenceHandler>> _loggerMock = new();

    private readonly ConfirmCorrespondenceHandler _handler;

    public ConfirmCorrespondenceHandlerTests()
    {
        _backgroundJobClientMock
            .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns("verify-job-id");

        _handler = new ConfirmCorrespondenceHandler(
            _altinnAuthorizationServiceMock.Object,
            _altinnRegisterServiceMock.Object,
            _correspondenceRepositoryMock.Object,
            _backgroundJobClientMock.Object,
            _dialogportenServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Process_SchedulesVerifyJobWithDelayAndPatchesDialog_ReturnsOk()
    {
        // Arrange
        var correspondence = new CorrespondenceEntityBuilder()
            .WithStatus(CorrespondenceStatus.Published)
            .WithStatus(CorrespondenceStatus.Fetched)
            .Build();

        var request = new ConfirmCorrespondenceRequest { CorrespondenceId = correspondence.Id };
        var user = CreateUserWithCallerUrn($"{UrnConstants.PersonIdAttribute}:12018012345");

        _correspondenceRepositoryMock
            .Setup(x => x.GetCorrespondenceById(correspondence.Id, true, false, false, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(correspondence);

        _altinnAuthorizationServiceMock
            .Setup(x => x.CheckAccessAsRecipient(user, correspondence, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _altinnRegisterServiceMock
            .Setup(x => x.LookUpPartyById(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Party { PartyUuid = Guid.NewGuid(), PartyId = 123 });

        _dialogportenServiceMock
            .Setup(x => x.PatchCorrespondenceDialogToConfirmed(correspondence.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Process(request, user, CancellationToken.None);

        // Assert
        Assert.True(result.IsT0);
        Assert.Equal(correspondence.Id, result.AsT0);

        _dialogportenServiceMock.Verify(x => x.PatchCorrespondenceDialogToConfirmed(correspondence.Id, It.IsAny<CancellationToken>()), Times.Once);

        _backgroundJobClientMock.Verify(x => x.Create(
            It.Is<Job>(job =>
                job.Type == typeof(VerifyCorrespondenceConfirmationHandler) &&
                job.Method.Name == nameof(VerifyCorrespondenceConfirmationHandler.VerifyPatchAndCommitConfirmation)),
            It.Is<IState>(state =>
                state is ScheduledState)), Times.Once);
    }

    private static ClaimsPrincipal CreateUserWithCallerUrn(string partyUrn)
    {
        var identity = new ClaimsIdentity([new Claim("c", partyUrn)], "TestAuthType");
        return new ClaimsPrincipal(identity);
    }
}
