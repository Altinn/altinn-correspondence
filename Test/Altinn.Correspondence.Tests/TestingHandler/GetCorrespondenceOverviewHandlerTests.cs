using Altinn.Correspondence.Application;
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
        private readonly Mock<IDialogportenService> _dialogportenServiceMock;
        private readonly Mock<IConfidentialReminderRepository> _confidentialReminderRepositoryMock;
        private readonly Mock<ILogger<GetCorrespondenceOverviewHandler>> _loggerMock;
        private readonly GetCorrespondenceOverviewHandler _handler;

        public GetCorrespondenceOverviewHandlerTests()
        {
            _altinnAuthorizationServiceMock = new Mock<IAltinnAuthorizationService>();
            _altinnRegisterServiceMock = new Mock<IAltinnRegisterService>();
            _correspondenceRepositoryMock = new Mock<ICorrespondenceRepository>();
            _correspondenceStatusRepositoryMock = new Mock<ICorrespondenceStatusRepository>();
            _backgroundJobClientMock = new Mock<IBackgroundJobClient>();
            _dialogportenServiceMock = new Mock<IDialogportenService>();
            _confidentialReminderRepositoryMock = new Mock<IConfidentialReminderRepository>();
            _loggerMock = new Mock<ILogger<GetCorrespondenceOverviewHandler>>();

            _handler = new GetCorrespondenceOverviewHandler(
                _altinnAuthorizationServiceMock.Object,
                _altinnRegisterServiceMock.Object,
                _confidentialReminderRepositoryMock.Object,
                _correspondenceRepositoryMock.Object,
                _correspondenceStatusRepositoryMock.Object,
                _backgroundJobClientMock.Object,
                _dialogportenServiceMock.Object,
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

        [Fact]
        public async Task Process_WhenConfidentialCorrespondenceOpenedAndIsLastReminderForRecipient_RemovesReminderAndSoftDeletesDialog()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            var partyUuid = Guid.NewGuid();
            var reminderDialogId = Guid.NewGuid();

            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)
                .Build();
            correspondence.Id = correspondenceId;
            correspondence.IsConfidential = true;

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

            // Mock confidential reminder - correspondence has a reminder
            _confidentialReminderRepositoryMock
                .Setup(x => x.CorrespondenceHasReminder(correspondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _confidentialReminderRepositoryMock
                .Setup(x => x.GetDialogIdOfReminderForRecipient(correspondence.Recipient, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid?)reminderDialogId);

            _confidentialReminderRepositoryMock
                .Setup(x => x.NumberOfRemindersForRecipient(correspondence.Recipient, It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            // Act
            await _handler.Process(request, user, CancellationToken.None);

            // Assert - reminder is removed
            _confidentialReminderRepositoryMock.Verify(
                x => x.RemoveConfidentialReminderByCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()),
                Times.Once);

            // Assert - dialog is soft deleted because recipient has no more confidential reminders
            _dialogportenServiceMock.Verify(
                x => x.TrySoftDeleteDialog(reminderDialogId.ToString()),
                Times.Once);
        }

        [Fact]
        public async Task Process_WhenConfidentialCorrespondenceOpenedAndRecipientHasOtherReminders_RemovesReminderButDoesNotSoftDeleteDialog()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            var partyUuid = Guid.NewGuid();
            var reminderDialogId = Guid.NewGuid();

            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)
                .Build();
            correspondence.Id = correspondenceId;
            correspondence.IsConfidential = true;

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

            // Mock confidential reminder - correspondence has a reminder
            _confidentialReminderRepositoryMock
                .Setup(x => x.CorrespondenceHasReminder(correspondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _confidentialReminderRepositoryMock
                .Setup(x => x.GetDialogIdOfReminderForRecipient(correspondence.Recipient, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid?)reminderDialogId);

            // Recipient still has other confidential reminders after removal
            _confidentialReminderRepositoryMock
                .Setup(x => x.NumberOfRemindersForRecipient(correspondence.Recipient, It.IsAny<CancellationToken>()))
                .ReturnsAsync(2);

            // Act
            await _handler.Process(request, user, CancellationToken.None);

            // Assert - reminder is still removed for this correspondence
            _confidentialReminderRepositoryMock.Verify(
                x => x.RemoveConfidentialReminderByCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()),
                Times.Once);

            // Assert - dialog is NOT soft deleted because recipient still has other confidential reminders
            _dialogportenServiceMock.Verify(
                x => x.TrySoftDeleteDialog(It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task Process_WhenNonConfidentialCorrespondenceOpened_DoesNotRunReminderLogic()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            var partyUuid = Guid.NewGuid();

            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)
                .Build();
            correspondence.Id = correspondenceId;
            correspondence.IsConfidential = false;

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

            // Assert - reminder repository is never consulted for non-confidential correspondence
            _confidentialReminderRepositoryMock.Verify(
                x => x.CorrespondenceHasReminder(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                Times.Never);

            _confidentialReminderRepositoryMock.Verify(
                x => x.RemoveConfidentialReminderByCorrespondenceId(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                Times.Never);

            _dialogportenServiceMock.Verify(
                x => x.TrySoftDeleteDialog(It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task Process_CorrespondenceNotFound_ReturnsNotFoundError()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            var request = new GetCorrespondenceOverviewRequest { CorrespondenceId = correspondenceId };

            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceById(correspondenceId, true, true, false, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync((CorrespondenceEntity?)null);

            // Act
            var result = await _handler.Process(request, new ClaimsPrincipal(), CancellationToken.None);

            // Assert
            Assert.True(result.IsT1);
            Assert.Equal(CorrespondenceErrors.CorrespondenceNotFound.ErrorCode, result.AsT1.ErrorCode);
            _altinnAuthorizationServiceMock.Verify(
                x => x.CheckAccessAsRecipient(It.IsAny<ClaimsPrincipal>(), It.IsAny<CorrespondenceEntity>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task Process_NeitherRecipientNorSenderAccess_ReturnsNoAccessError()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)
                .Build();
            correspondence.Id = correspondenceId;

            var request = new GetCorrespondenceOverviewRequest { CorrespondenceId = correspondenceId };

            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceById(correspondenceId, true, true, false, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(correspondence);
            _altinnAuthorizationServiceMock
                .Setup(x => x.CheckAccessAsRecipient(It.IsAny<ClaimsPrincipal>(), It.IsAny<CorrespondenceEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _altinnAuthorizationServiceMock
                .Setup(x => x.CheckAccessAsSender(It.IsAny<ClaimsPrincipal>(), It.IsAny<CorrespondenceEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _handler.Process(request, new ClaimsPrincipal(), CancellationToken.None);

            // Assert
            Assert.True(result.IsT1);
            Assert.Equal(AuthorizationErrors.NoAccessToResource.ErrorCode, result.AsT1.ErrorCode);
        }

        [Fact]
        public async Task Process_WhenConfidentialCorrespondenceHasNoReminder_SkipsReminderCleanup()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            var partyUuid = Guid.NewGuid();

            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)
                .Build();
            correspondence.Id = correspondenceId;
            correspondence.IsConfidential = true;

            var user = new ClaimsPrincipal();
            var request = new GetCorrespondenceOverviewRequest
            {
                CorrespondenceId = correspondenceId,
                OnlyGettingContent = false
            };

            _altinnAuthorizationServiceMock
                .Setup(x => x.CheckAccessAsRecipient(It.IsAny<ClaimsPrincipal>(), It.IsAny<CorrespondenceEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _altinnAuthorizationServiceMock
                .Setup(x => x.CheckAccessAsSender(It.IsAny<ClaimsPrincipal>(), It.IsAny<CorrespondenceEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _altinnRegisterServiceMock
                .Setup(x => x.LookUpPartyById(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Party { PartyUuid = partyUuid });
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceById(correspondenceId, true, true, false, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(correspondence);

            _confidentialReminderRepositoryMock
                .Setup(x => x.CorrespondenceHasReminder(correspondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            await _handler.Process(request, user, CancellationToken.None);

            // Assert
            _confidentialReminderRepositoryMock.Verify(
                x => x.RemoveConfidentialReminderByCorrespondenceId(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                Times.Never);
            _dialogportenServiceMock.Verify(
                x => x.TrySoftDeleteDialog(It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task Process_WhenConfidentialCorrespondenceOpenedAndIsLastReminderButNoDialogId_RemovesReminderWithoutSoftDelete()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            var partyUuid = Guid.NewGuid();

            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)
                .Build();
            correspondence.Id = correspondenceId;
            correspondence.IsConfidential = true;

            var user = new ClaimsPrincipal();
            var request = new GetCorrespondenceOverviewRequest
            {
                CorrespondenceId = correspondenceId,
                OnlyGettingContent = false
            };

            _altinnAuthorizationServiceMock
                .Setup(x => x.CheckAccessAsRecipient(It.IsAny<ClaimsPrincipal>(), It.IsAny<CorrespondenceEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _altinnAuthorizationServiceMock
                .Setup(x => x.CheckAccessAsSender(It.IsAny<ClaimsPrincipal>(), It.IsAny<CorrespondenceEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _altinnRegisterServiceMock
                .Setup(x => x.LookUpPartyById(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Party { PartyUuid = partyUuid });
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceById(correspondenceId, true, true, false, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(correspondence);

            _confidentialReminderRepositoryMock
                .Setup(x => x.CorrespondenceHasReminder(correspondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _confidentialReminderRepositoryMock
                .Setup(x => x.NumberOfRemindersForRecipient(correspondence.Recipient, It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);
            // Dialog ID is missing
            _confidentialReminderRepositoryMock
                .Setup(x => x.GetDialogIdOfReminderForRecipient(correspondence.Recipient, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid?)null);

            // Act
            await _handler.Process(request, user, CancellationToken.None);

            // Assert - reminder is removed even without a dialog ID
            _confidentialReminderRepositoryMock.Verify(
                x => x.RemoveConfidentialReminderByCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()),
                Times.Once);

            // Assert - dialog is NOT soft-deleted because there was no dialog ID to delete
            _dialogportenServiceMock.Verify(
                x => x.TrySoftDeleteDialog(It.IsAny<string>()),
                Times.Never);
        }
    }
} 