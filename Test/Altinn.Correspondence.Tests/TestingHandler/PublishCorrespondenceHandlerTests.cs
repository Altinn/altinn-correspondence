using Altinn.Correspondence.Application.PublishCorrespondence;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Application.SendSlackNotification;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Logging;
using Moq;
using Altinn.Correspondence.Tests.Extensions;

namespace Altinn.Correspondence.Tests.TestingHandler
{
    public class PublishCorrespondenceHandlerTests
    {
        private readonly Mock<IAltinnRegisterService> _altinnRegisterServiceMock;
        private readonly Mock<ILogger<PublishCorrespondenceHandler>> _loggerMock;
        private readonly Mock<ICorrespondenceRepository> _correspondenceRepositoryMock;
        private readonly Mock<ICorrespondenceStatusRepository> _correspondenceStatusRepositoryMock;
        private readonly Mock<IContactReservationRegistryService> _contactReservationRegistryServiceMock;
        private readonly Mock<IBackgroundJobClient> _backgroundJobClientMock;
        private readonly Mock<IIdempotencyKeyRepository> _idempotencyKeyRepositoryMock;
        private readonly PublishCorrespondenceHandler _handler;

        public PublishCorrespondenceHandlerTests()
        {
            _altinnRegisterServiceMock = new Mock<IAltinnRegisterService>();
            _loggerMock = new Mock<ILogger<PublishCorrespondenceHandler>>();
            _correspondenceRepositoryMock = new Mock<ICorrespondenceRepository>();
            _correspondenceStatusRepositoryMock = new Mock<ICorrespondenceStatusRepository>();
            _contactReservationRegistryServiceMock = new Mock<IContactReservationRegistryService>();
            _backgroundJobClientMock = new Mock<IBackgroundJobClient>();
            _idempotencyKeyRepositoryMock = new Mock<IIdempotencyKeyRepository>();
            _backgroundJobClientMock
                .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
                .Returns(() => Guid.NewGuid().ToString());
            _idempotencyKeyRepositoryMock
                .Setup(x => x.CreateAsync(It.IsAny<IdempotencyKeyEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IdempotencyKeyEntity key, CancellationToken _) => key);
            _idempotencyKeyRepositoryMock
                .Setup(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _handler = new PublishCorrespondenceHandler(
                _altinnRegisterServiceMock.Object,
                _loggerMock.Object,
                _correspondenceRepositoryMock.Object,
                _correspondenceStatusRepositoryMock.Object,
                _contactReservationRegistryServiceMock.Object,
                _backgroundJobClientMock.Object,
                _idempotencyKeyRepositoryMock.Object);
        }

        private void SetupCommonMocks(Guid correspondenceId, Guid partyUuid, CorrespondenceEntity correspondence)
        {
            _altinnRegisterServiceMock.SetupPartyByIdLookup("313721779", Guid.NewGuid());

            _altinnRegisterServiceMock.SetupPartyByIdLookup("310244007", partyUuid);

            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceById(correspondenceId, true, true, false, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(correspondence);

            _correspondenceRepositoryMock
                .Setup(x => x.AreAllAttachmentsPublished(correspondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _altinnRegisterServiceMock.SetupEmptyMainUnitsLookup("310244007");
        }

        private CorrespondenceEntity CreateTestCorrespondence(
            Guid correspondenceId, 
            string senderUrn, 
            string recipientUrn, 
            bool isConfidential = true,
            DateTimeOffset? requestedPublishTime = null)
        {
            var now = DateTimeOffset.UtcNow;
            return new CorrespondenceEntity
            {
                Id = correspondenceId,
                Sender = senderUrn,
                Recipient = recipientUrn,
                IsConfidential = isConfidential,
                ResourceId = "resource-123",
                SendersReference = "ref-123",
                RequestedPublishTime = requestedPublishTime ?? now.AddMinutes(-10),
                Created = now.AddMinutes(-30),
                ExternalReferences = new List<ExternalReferenceEntity>
                {
                    new ExternalReferenceEntity
                    {
                        ReferenceType = ReferenceType.DialogportenDialogId,
                        ReferenceValue = "dialog-123"
                    }
                },
                Statuses = new List<CorrespondenceStatusEntity>
                {
                    new CorrespondenceStatusEntity
                    {
                        CorrespondenceId = correspondenceId,
                        Status = CorrespondenceStatus.ReadyForPublish,
                        StatusChanged = now.AddMinutes(-5),
                        StatusText = CorrespondenceStatus.ReadyForPublish.ToString()
                    }
                }
            };
        }

        [Fact]
        public async Task Process_CorrespondenceWithOrgRecipientMissingRequiredRoles_FailsCorrespondence()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            var partyUuid = Guid.NewGuid();
            var senderUrn = "urn:altinn:organization:identifier-no:313721779";
            var recipientUrn = "urn:altinn:organization:identifier-no:310244007";
            
            var correspondence = CreateTestCorrespondence(correspondenceId, senderUrn, recipientUrn);
            SetupCommonMocks(correspondenceId, partyUuid, correspondence);
            _altinnRegisterServiceMock.SetupPartyRoleLookup(partyUuid.ToString(), "ANNET");

            // Act
            await _handler.Process(correspondenceId, null, CancellationToken.None);

            // Assert
            _correspondenceStatusRepositoryMock.Verify(
                x => x.AddCorrespondenceStatus(
                    It.Is<CorrespondenceStatusEntity>(s => 
                        s.CorrespondenceId == correspondenceId && 
                        s.Status == CorrespondenceStatus.Failed && 
                        s.StatusText.Contains("lacks roles")),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _backgroundJobClientMock.Verify(
                x => x.Create(
                    It.Is<Job>(job => job.Type == typeof(SendSlackNotificationHandler) && job.Method.Name == nameof(SendSlackNotificationHandler.Process)),
                    It.IsAny<IState>()),
                Times.AtLeastOnce);

            _idempotencyKeyRepositoryMock.Verify(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Process_CorrespondenceWithOrgRecipientHavingRequiredRoles_Succeeds()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            var partyUuid = Guid.NewGuid();
            var senderUrn = "urn:altinn:organization:identifier-no:313721779";
            var recipientUrn = "urn:altinn:organization:identifier-no:310244007";
            
            var correspondence = CreateTestCorrespondence(correspondenceId, senderUrn, recipientUrn);
            SetupCommonMocks(correspondenceId, partyUuid, correspondence);
            _altinnRegisterServiceMock.SetupPartyRoleLookup(partyUuid.ToString(), "daglig-leder");

            // Act
            await _handler.Process(correspondenceId, null, CancellationToken.None);

            // Assert
            _correspondenceStatusRepositoryMock.Verify(
                x => x.AddCorrespondenceStatus(
                    It.Is<CorrespondenceStatusEntity>(s => 
                        s.CorrespondenceId == correspondenceId && 
                        s.Status == CorrespondenceStatus.Published),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _correspondenceRepositoryMock.Verify(
                x => x.UpdatePublished(correspondenceId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
                Times.Once);

            _backgroundJobClientMock.Verify(
                x => x.Create(
                    It.Is<Job>(job => job.Type == typeof(SendSlackNotificationHandler) && job.Method.Name == nameof(SendSlackNotificationHandler.Process)),
                    It.IsAny<IState>()),
                Times.Never);

            _idempotencyKeyRepositoryMock.Verify(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Process_CorrespondenceWithSubunitRecipient_MainUnitMissingRequiredRoles_FailsCorrespondence()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            var partyUuid = Guid.NewGuid();
            var mainUnitUuid = Guid.NewGuid();
            var senderUrn = "urn:altinn:organization:identifier-no:313721779";
            var recipientUrn = "urn:altinn:organization:identifier-no:310244007"; // subunit URN

            var correspondence = CreateTestCorrespondence(correspondenceId, senderUrn, recipientUrn);
            SetupCommonMocks(correspondenceId, partyUuid, correspondence);

            // Recipient is org, so roles are checked via main unit
            _altinnRegisterServiceMock.SetupMainUnitsLookup("310244007", "310244007", mainUnitUuid);
            _altinnRegisterServiceMock.SetupPartyRoleLookup(mainUnitUuid.ToString(), "ANNET");

            // Act
            await _handler.Process(correspondenceId, null, CancellationToken.None);

            // Assert
            _correspondenceStatusRepositoryMock.Verify(
                x => x.AddCorrespondenceStatus(
                    It.Is<CorrespondenceStatusEntity>(s =>
                        s.CorrespondenceId == correspondenceId &&
                        s.Status == CorrespondenceStatus.Failed &&
                        s.StatusText.Contains("lacks roles")),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Process_CorrespondenceWithSubunitRecipient_MainUnitHasRequiredRoles_Succeeds()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            var partyUuid = Guid.NewGuid();
            var mainUnitUuid = Guid.NewGuid();
            var senderUrn = "urn:altinn:organization:identifier-no:313721779";
            var recipientUrn = "urn:altinn:organization:identifier-no:310244007"; // subunit URN

            var correspondence = CreateTestCorrespondence(correspondenceId, senderUrn, recipientUrn);
            SetupCommonMocks(correspondenceId, partyUuid, correspondence);

            _altinnRegisterServiceMock.SetupMainUnitsLookup("310244007", "310244007", mainUnitUuid);
            _altinnRegisterServiceMock.SetupPartyRoleLookup(mainUnitUuid.ToString(), "daglig-leder");

            // Act
            await _handler.Process(correspondenceId, null, CancellationToken.None);

            // Assert
            _correspondenceStatusRepositoryMock.Verify(
                x => x.AddCorrespondenceStatus(
                    It.Is<CorrespondenceStatusEntity>(s =>
                        s.CorrespondenceId == correspondenceId &&
                        s.Status == CorrespondenceStatus.Published),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _correspondenceRepositoryMock.Verify(
                x => x.UpdatePublished(correspondenceId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
} 