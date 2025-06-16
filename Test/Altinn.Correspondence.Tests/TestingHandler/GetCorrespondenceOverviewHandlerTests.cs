using Altinn.Correspondence.Application.GetCorrespondenceOverview;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Tests.Factories;
using Hangfire;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace Altinn.Correspondence.Tests.TestingHandler
{
    public class GetCorrespondenceOverviewHandlerTests
    {
        private readonly Mock<IAltinnAuthorizationService> _altinnAuthorizationServiceMock;
        private readonly Mock<IAltinnRegisterService> _altinnRegisterServiceMock;
        private readonly Mock<ICorrespondenceRepository> _correspondenceRepositoryMock;
        private readonly Mock<ICorrespondenceStatusRepository> _correspondenceStatusRepositoryMock;
        private readonly Mock<IBackgroundJobClient> _backgroundJobClientMock;
        private readonly Mock<ILogger<GetCorrespondenceOverviewHandler>> _loggerMock;
        private readonly GetCorrespondenceOverviewHandler _handler;

        public GetCorrespondenceOverviewHandlerTests()
        {
            _altinnAuthorizationServiceMock = new Mock<IAltinnAuthorizationService>();
            _altinnRegisterServiceMock = new Mock<IAltinnRegisterService>();
            _correspondenceRepositoryMock = new Mock<ICorrespondenceRepository>();
            _correspondenceStatusRepositoryMock = new Mock<ICorrespondenceStatusRepository>();
            _backgroundJobClientMock = new Mock<IBackgroundJobClient>();
            _loggerMock = new Mock<ILogger<GetCorrespondenceOverviewHandler>>();

            _handler = new GetCorrespondenceOverviewHandler(
                _altinnAuthorizationServiceMock.Object,
                _altinnRegisterServiceMock.Object,
                _correspondenceRepositoryMock.Object,
                _correspondenceStatusRepositoryMock.Object,
                _backgroundJobClientMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task Process_WhenOnlyGettingContentAndNotRead_AddsReadStatus()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            var partyUuid = Guid.NewGuid();
            
            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)
                .Build();
            correspondence.Id = correspondenceId;

            var user = new ClaimsPrincipal();
            var request = new GetCorrespondenceOverviewRequest 
            { 
                CorrespondenceId = correspondenceId,
                OnlyGettingContent = true
            };

            // Mock authorization
            _altinnAuthorizationServiceMock
                .Setup(x => x.CheckAccessAsRecipient(It.IsAny<ClaimsPrincipal>(), It.IsAny<CorrespondenceEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _altinnAuthorizationServiceMock
                .Setup(x => x.CheckAccessAsSender(It.IsAny<ClaimsPrincipal>(), It.IsAny<CorrespondenceEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Mock party lookup
            _altinnRegisterServiceMock
                .Setup(x => x.LookUpPartyById(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Party { PartyUuid = partyUuid });

            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceById(correspondenceId, true, true, false, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(correspondence);

            // Act
            await _handler.Process(request, user, CancellationToken.None);

            // Assert
            _correspondenceStatusRepositoryMock.Verify(
                x => x.AddCorrespondenceStatus(
                    It.Is<CorrespondenceStatusEntity>(s => 
                        s.CorrespondenceId == correspondenceId && 
                        s.Status == CorrespondenceStatus.Read),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Process_WhenOnlyGettingContentAndAlreadyRead_DoesNotAddReadStatus()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            var partyUuid = Guid.NewGuid();
            
            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)
                .WithStatus(CorrespondenceStatus.Read)
                .Build();
            correspondence.Id = correspondenceId;

            var user = new ClaimsPrincipal();
            var request = new GetCorrespondenceOverviewRequest 
            { 
                CorrespondenceId = correspondenceId,
                OnlyGettingContent = true
            };

            // Mock authorization
            _altinnAuthorizationServiceMock
                .Setup(x => x.CheckAccessAsRecipient(It.IsAny<ClaimsPrincipal>(), It.IsAny<CorrespondenceEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _altinnAuthorizationServiceMock
                .Setup(x => x.CheckAccessAsSender(It.IsAny<ClaimsPrincipal>(), It.IsAny<CorrespondenceEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Mock party lookup
            _altinnRegisterServiceMock
                .Setup(x => x.LookUpPartyById(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Party { PartyUuid = partyUuid });

            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceById(correspondenceId, true, true, false, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(correspondence);

            // Act
            await _handler.Process(request, user, CancellationToken.None);

            // Assert
            _correspondenceStatusRepositoryMock.Verify(
                x => x.AddCorrespondenceStatus(
                    It.Is<CorrespondenceStatusEntity>(s => 
                        s.CorrespondenceId == correspondenceId && 
                        s.Status == CorrespondenceStatus.Read),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task Process_WhenNotOnlyGettingContent_DoesNotAddReadStatus()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            var partyUuid = Guid.NewGuid();
            
            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)
                .Build();
            correspondence.Id = correspondenceId;

            var user = new ClaimsPrincipal();
            var request = new GetCorrespondenceOverviewRequest 
            { 
                CorrespondenceId = correspondenceId,
                OnlyGettingContent = false
            };

            // Mock authorization
            _altinnAuthorizationServiceMock
                .Setup(x => x.CheckAccessAsRecipient(It.IsAny<ClaimsPrincipal>(), It.IsAny<CorrespondenceEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _altinnAuthorizationServiceMock
                .Setup(x => x.CheckAccessAsSender(It.IsAny<ClaimsPrincipal>(), It.IsAny<CorrespondenceEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Mock party lookup
            _altinnRegisterServiceMock
                .Setup(x => x.LookUpPartyById(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Party { PartyUuid = partyUuid });

            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceById(correspondenceId, true, true, false, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(correspondence);

            // Act
            await _handler.Process(request, user, CancellationToken.None);

            // Assert
            _correspondenceStatusRepositoryMock.Verify(
                x => x.AddCorrespondenceStatus(
                    It.Is<CorrespondenceStatusEntity>(s => 
                        s.CorrespondenceId == correspondenceId && 
                        s.Status == CorrespondenceStatus.Read),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }
} 