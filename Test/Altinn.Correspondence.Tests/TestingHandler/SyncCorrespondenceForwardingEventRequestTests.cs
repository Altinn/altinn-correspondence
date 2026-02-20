using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.PurgeCorrespondence;
using Altinn.Correspondence.Application.SyncCorrespondenceEvent;
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

namespace Altinn.Correspondence.Tests.TestingHandler
{
    public class SyncCorrespondenceForwardingEventRequestTests
    {
        private readonly Mock<ICorrespondenceRepository> _correspondenceRepositoryMock;
        private readonly Mock<ICorrespondenceForwardingEventRepository> _forwardingEventRepositoryMock;
        private readonly Mock<IBackgroundJobClient> _backgroundJobClientMock;
        private readonly Mock<ILogger<SyncCorrespondenceForwardingEventHandler>> _loggerMock;
        private readonly Mock<ICorrespondenceStatusRepository> _correspondenceStatusRepositoryMock;
        private readonly Mock<ICorrespondenceDeleteEventRepository> _correspondenceDeleteRepositoryMock;
        private readonly Mock<ICorrespondenceNotificationRepository> _correspondenceNotificationRepositoryMock;
        private readonly Mock<IAltinnRegisterService> _altinnRegisterServiceMock;
        private readonly Mock<IAttachmentRepository> _attachmentRepositoryMock;
        private readonly Mock<IAttachmentStatusRepository> _attachmentStatusRepositoryMock;
        private readonly Mock<IDialogportenService> _dialogportenServiceMock;
        private readonly Mock<ILogger<CorrespondenceMigrationEventHelper>> _eventHelperLoggerMock;
        private readonly SyncCorrespondenceForwardingEventHandler _handler;

        public SyncCorrespondenceForwardingEventRequestTests()
        {
            _correspondenceRepositoryMock = new Mock<ICorrespondenceRepository>();
            _forwardingEventRepositoryMock = new Mock<ICorrespondenceForwardingEventRepository>();
            _backgroundJobClientMock = new Mock<IBackgroundJobClient>();
            _loggerMock = new Mock<ILogger<SyncCorrespondenceForwardingEventHandler>>();
            
            // Setup mocks for CorrespondenceMigrationEventHelper dependencies
            _correspondenceStatusRepositoryMock = new Mock<ICorrespondenceStatusRepository>();
            _correspondenceDeleteRepositoryMock = new Mock<ICorrespondenceDeleteEventRepository>();
            _correspondenceNotificationRepositoryMock = new Mock<ICorrespondenceNotificationRepository>();
            _altinnRegisterServiceMock = new Mock<IAltinnRegisterService>();
            _attachmentRepositoryMock = new Mock<IAttachmentRepository>();
            _attachmentStatusRepositoryMock = new Mock<IAttachmentStatusRepository>();
            _dialogportenServiceMock = new Mock<IDialogportenService>();
            _eventHelperLoggerMock = new Mock<ILogger<CorrespondenceMigrationEventHelper>>();
            
            var purgeCorrespondenceHelper = new PurgeCorrespondenceHelper(
                _attachmentRepositoryMock.Object,
                _attachmentStatusRepositoryMock.Object,
                _correspondenceStatusRepositoryMock.Object,
                _backgroundJobClientMock.Object,
                _dialogportenServiceMock.Object,
                _correspondenceRepositoryMock.Object);

            var correspondenceMigrationEventHelper = new CorrespondenceMigrationEventHelper(
                _correspondenceStatusRepositoryMock.Object,
                _correspondenceDeleteRepositoryMock.Object,
                _correspondenceNotificationRepositoryMock.Object,
                _forwardingEventRepositoryMock.Object,
                _altinnRegisterServiceMock.Object,
                purgeCorrespondenceHelper,
                _backgroundJobClientMock.Object,
                _eventHelperLoggerMock.Object);

            _handler = new SyncCorrespondenceForwardingEventHandler(
                _correspondenceRepositoryMock.Object,
                correspondenceMigrationEventHelper,
                _backgroundJobClientMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task Process_NewForwardingEvents_AddedOK()
        {
            // Arrange            
            var partyUuid = Guid.NewGuid();

            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)                
                .WithAltinn2CorrespondenceId(12345)                
                .WithIsMigrating(true) // Not available in Altinn 3 APIs
                .Build();
            var correspondenceId = correspondence.Id;

            var request = new SyncCorrespondenceForwardingEventRequest
            {
                CorrespondenceId = correspondenceId,
                SyncedEvents = new List<CorrespondenceForwardingEventEntity>
                {
                    new CorrespondenceForwardingEventEntity
                    {
                        // Example of Copy sent to own email address
                        ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 11, 0, 0)),
                        ForwardedByPartyUuid = partyUuid,
                        ForwardedByUserId = 123,
                        ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                        ForwardedToEmailAddress = "user1@awesometestusers.com",
                        ForwardingText = "Keep this as a backup in my email."
                    },
                    new CorrespondenceForwardingEventEntity
                    {
                        // Example of Copy sent to own digital mailbox
                        ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 11, 5, 0)),
                        ForwardedByPartyUuid = partyUuid,
                        ForwardedByUserId = 123,
                        ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                        MailboxSupplier = "urn:altinn:organization:identifier-no:123456789"
                    },
                    new CorrespondenceForwardingEventEntity
                    {
                        // Example of Instance Delegation by User 1 to User2
                        ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 12, 15, 0)),
                        ForwardedByPartyUuid = partyUuid,
                        ForwardedByUserId = 123,
                        ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                        ForwardedToUserId = 456,
                        ForwardedToUserUuid = new Guid("1D5FD16E-2905-414A-AC97-844929975F17"),
                        ForwardingText = "User2, - look into this for me please. - User1.",
                        ForwardedToEmailAddress  = "user2@awesometestusers.com"
                    }
                }
            };

            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceByIdForSync(correspondenceId, It.IsAny<CorrespondenceSyncType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(correspondence);
            // Mock forwarding event repository
            _forwardingEventRepositoryMock
                .Setup(x => x.AddForwardingEventForSync(It.IsAny<CorrespondenceForwardingEventEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Guid.NewGuid());

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceByIdForSync(correspondenceId, CorrespondenceSyncType.ForwardingEvents, It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();

           _forwardingEventRepositoryMock.Verify(
                x => x.AddForwardingEventForSync(
                    It.Is<CorrespondenceForwardingEventEntity>(f => 
                        f.ForwardedToEmailAddress == "user1@awesometestusers.com" &&
                        f.ForwardingText == "Keep this as a backup in my email." &&
                        f.ForwardedByPartyUuid == partyUuid &&
                        f.ForwardedByUserId == 123 &&
                        f.ForwardedByUserUuid == new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E")),
                It.IsAny<CancellationToken>()),
            Times.Once
            );
            _forwardingEventRepositoryMock.Verify(
                x => x.AddForwardingEventForSync(
                    It.Is<CorrespondenceForwardingEventEntity>(f => 
                        f.MailboxSupplier == "urn:altinn:organization:identifier-no:123456789" &&
                        f.ForwardedByPartyUuid == partyUuid &&
                        f.ForwardedByUserId == 123 &&
                        f.ForwardedByUserUuid == new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E")),
                It.IsAny<CancellationToken>()),
            Times.Once
            );
            _forwardingEventRepositoryMock.Verify(
                x => x.AddForwardingEventForSync(
                    It.Is<CorrespondenceForwardingEventEntity>(f => 
                        f.ForwardedToUserId == 456 &&
                        f.ForwardedToUserUuid == new Guid("1D5FD16E-2905-414A-AC97-844929975F17") &&
                        f.ForwardingText == "User2, - look into this for me please. - User1." &&
                        f.ForwardedToEmailAddress == "user2@awesometestusers.com" &&
                        f.ForwardedByPartyUuid == partyUuid &&
                        f.ForwardedByUserId == 123 &&
                        f.ForwardedByUserUuid == new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E")),
                It.IsAny<CancellationToken>()),
            Times.Once
            );
            _forwardingEventRepositoryMock.VerifyNoOtherCalls();

            _backgroundJobClientMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Process_Available_NewForwardingEvents_AddedOK_WithDPUpdate()
        {
            // Arrange            
            var partyUuid = Guid.NewGuid();

            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)
                .WithAltinn2CorrespondenceId(12345)
                .WithIsMigrating(false) // Availabe in Altinn 3 APIs and Dialoporten
                .WithDialogId("123")
                .Build();
            var correspondenceId = correspondence.Id;

            DateTimeOffset fwdDate1 = new DateTimeOffset(new DateTime(2024, 1, 6, 11, 0, 0));
            Guid fwdId1 = Guid.NewGuid();
            DateTimeOffset fwdDate2 = new DateTimeOffset(new DateTime(2024, 1, 6, 11, 5, 0));
            Guid fwdId2 = Guid.NewGuid();
            DateTimeOffset fwdDate3 = new DateTimeOffset(new DateTime(2024, 1, 6, 12, 15, 0));
            Guid fwdId3 = Guid.NewGuid();

            var request = new SyncCorrespondenceForwardingEventRequest
            {
                CorrespondenceId = correspondenceId,
                SyncedEvents = new List<CorrespondenceForwardingEventEntity>
                {
                    new CorrespondenceForwardingEventEntity
                    {
                        // Example of Copy sent to own email address
                        ForwardedOnDate = fwdDate1,
                        ForwardedByPartyUuid = partyUuid,
                        ForwardedByUserId = 123,
                        ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                        ForwardedToEmailAddress = "user1@awesometestusers.com",
                        ForwardingText = "Keep this as a backup in my email."
                    },
                    new CorrespondenceForwardingEventEntity
                    {
                        // Example of Copy sent to own digital mailbox
                        ForwardedOnDate = fwdDate2,
                        ForwardedByPartyUuid = partyUuid,
                        ForwardedByUserId = 123,
                        ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                        MailboxSupplier = "urn:altinn:organization:identifier-no:123456789"
                    },
                    new CorrespondenceForwardingEventEntity
                    {
                        // Example of Instance Delegation by User 1 to User2
                        ForwardedOnDate = fwdDate3,
                        ForwardedByPartyUuid = partyUuid,
                        ForwardedByUserId = 123,
                        ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                        ForwardedToUserId = 456,
                        ForwardedToUserUuid = new Guid("1D5FD16E-2905-414A-AC97-844929975F17"),
                        ForwardingText = "User2, - look into this for me please. - User1.",
                        ForwardedToEmailAddress  = "user2@awesometestusers.com"
                    }
                }
            };

            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceByIdForSync(correspondenceId, It.IsAny<CorrespondenceSyncType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(correspondence);
            // Mock forwarding event repository
            _forwardingEventRepositoryMock
                .Setup(x => x.AddForwardingEventForSync(It.Is<CorrespondenceForwardingEventEntity>(f => f.ForwardedOnDate == fwdDate1), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fwdId1);
            _forwardingEventRepositoryMock
                .Setup(x => x.AddForwardingEventForSync(It.Is<CorrespondenceForwardingEventEntity>(f => f.ForwardedOnDate == fwdDate2), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fwdId2);
            _forwardingEventRepositoryMock
                .Setup(x => x.AddForwardingEventForSync(It.Is<CorrespondenceForwardingEventEntity>(f => f.ForwardedOnDate == fwdDate3), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fwdId3);

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceByIdForSync(correspondenceId, CorrespondenceSyncType.ForwardingEvents, It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();

            _forwardingEventRepositoryMock.Verify(
                 x => x.AddForwardingEventForSync(
                     It.Is<CorrespondenceForwardingEventEntity>(f =>
                         f.ForwardedToEmailAddress == "user1@awesometestusers.com" &&
                         f.ForwardingText == "Keep this as a backup in my email." &&
                         f.ForwardedByPartyUuid == partyUuid &&
                         f.ForwardedByUserId == 123 &&
                         f.ForwardedByUserUuid == new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E")),
                 It.IsAny<CancellationToken>()),
             Times.Once
             );
            _forwardingEventRepositoryMock.Verify(
                x => x.AddForwardingEventForSync(
                    It.Is<CorrespondenceForwardingEventEntity>(f =>
                        f.MailboxSupplier == "urn:altinn:organization:identifier-no:123456789" &&
                        f.ForwardedByPartyUuid == partyUuid &&
                        f.ForwardedByUserId == 123 &&
                        f.ForwardedByUserUuid == new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E")),
                It.IsAny<CancellationToken>()),
            Times.Once
            );
            _forwardingEventRepositoryMock.Verify(
                x => x.AddForwardingEventForSync(
                    It.Is<CorrespondenceForwardingEventEntity>(f =>
                        f.ForwardedToUserId == 456 &&
                        f.ForwardedToUserUuid == new Guid("1D5FD16E-2905-414A-AC97-844929975F17") &&
                        f.ForwardingText == "User2, - look into this for me please. - User1." &&
                        f.ForwardedToEmailAddress == "user2@awesometestusers.com" &&
                        f.ForwardedByPartyUuid == partyUuid &&
                        f.ForwardedByUserId == 123 &&
                        f.ForwardedByUserUuid == new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E")),
                It.IsAny<CancellationToken>()),
            Times.Once
            );
            _forwardingEventRepositoryMock.VerifyNoOtherCalls();

            // Verify that BackgroundJobClient was called to update Dialogporten for the new forwarding events
            _backgroundJobClientMock.Verify(x => x.Create(
                It.Is<Job>(job => job.Method.Name == nameof(IDialogportenService.AddForwardingEvent) && (Guid)job.Args[0] == fwdId1),
                It.IsAny<EnqueuedState>()));
            _backgroundJobClientMock.Verify(x => x.Create(
                It.Is<Job>(job => job.Method.Name == nameof(IDialogportenService.AddForwardingEvent) && (Guid)job.Args[0] == fwdId2),
                It.IsAny<EnqueuedState>()));
            _backgroundJobClientMock.Verify(x => x.Create(
                It.Is<Job>(job => job.Method.Name == nameof(IDialogportenService.AddForwardingEvent) && (Guid)job.Args[0] == fwdId3),
                It.IsAny<EnqueuedState>()));
            _backgroundJobClientMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Process_DuplicateForwardingEvents_NoneAdded()
        {
            // Arrange            
            var partyUuid = Guid.NewGuid();

            DateTimeOffset fwdEvent01Date = new DateTimeOffset(new DateTime(2024, 1, 6, 11, 0, 0));
            DateTimeOffset fwdEvent02Date = new DateTimeOffset(new DateTime(2024, 1, 6, 11, 5, 0));
            DateTimeOffset fwdEvent03Date = new DateTimeOffset(new DateTime(2024, 1, 6, 12, 15, 0));

            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)
                .WithAltinn2CorrespondenceId(12345)
                .WithIsMigrating(true) // Not available in Altinn 3 APIs
                .WithForwardingEvents(new List<CorrespondenceForwardingEventEntity>() { new CorrespondenceForwardingEventEntity
                    {
                        // Example of Copy sent to own email address
                        ForwardedOnDate = fwdEvent01Date,
                        ForwardedByPartyUuid = partyUuid,
                        ForwardedByUserId = 123,
                        ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                        ForwardedToEmailAddress = "user1@awesometestusers.com",
                        ForwardingText = "Keep this as a backup in my email."
                    },
                    new CorrespondenceForwardingEventEntity
                    {
                        // Example of Copy sent to own digital mailbox
                        ForwardedOnDate = fwdEvent02Date,
                        ForwardedByPartyUuid = partyUuid,
                        ForwardedByUserId = 123,
                        ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                        MailboxSupplier = "urn:altinn:organization:identifier-no:123456789"
                    },
                    new CorrespondenceForwardingEventEntity
                    {
                        // Example of Instance Delegation by User 1 to User2
                        ForwardedOnDate = fwdEvent03Date,
                        ForwardedByPartyUuid = partyUuid,
                        ForwardedByUserId = 123,
                        ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                        ForwardedToUserId = 456,
                        ForwardedToUserUuid = new Guid("1D5FD16E-2905-414A-AC97-844929975F17"),
                        ForwardingText = "User2, - look into this for me please. - User1.",
                        ForwardedToEmailAddress  = "user2@awesometestusers.com"
                    }
                })
                .Build();
            var correspondenceId = correspondence.Id;

            var request = new SyncCorrespondenceForwardingEventRequest
            {
                CorrespondenceId = correspondenceId,
                SyncedEvents = new List<CorrespondenceForwardingEventEntity>
                {
                    new CorrespondenceForwardingEventEntity
                    {
                        // Example of Copy sent to own email address
                        ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 11, 0, 0)),
                        ForwardedByPartyUuid = partyUuid,
                        ForwardedByUserId = 123,
                        ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                        ForwardedToEmailAddress = "user1@awesometestusers.com",
                        ForwardingText = "Keep this as a backup in my email."
                    },
                    new CorrespondenceForwardingEventEntity
                    {
                        // Example of Copy sent to own digital mailbox
                        ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 11, 5, 0)),
                        ForwardedByPartyUuid = partyUuid,
                        ForwardedByUserId = 123,
                        ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                        MailboxSupplier = "urn:altinn:organization:identifier-no:123456789"
                    },
                    new CorrespondenceForwardingEventEntity
                    {
                        // Example of Instance Delegation by User 1 to User2
                        ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 12, 15, 0)),
                        ForwardedByPartyUuid = partyUuid,
                        ForwardedByUserId = 123,
                        ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                        ForwardedToUserId = 456,
                        ForwardedToUserUuid = new Guid("1D5FD16E-2905-414A-AC97-844929975F17"),
                        ForwardingText = "User2, - look into this for me please. - User1.",
                        ForwardedToEmailAddress  = "user2@awesometestusers.com"
                    }
                }
            };

            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceByIdForSync(correspondenceId, It.IsAny<CorrespondenceSyncType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(correspondence);

            _forwardingEventRepositoryMock
                .Setup(x => x.AddForwardingEventForSync(It.Is<CorrespondenceForwardingEventEntity>(f => f.ForwardedOnDate == fwdEvent01Date), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Guid.Empty); // Simulate duplicate event
            _forwardingEventRepositoryMock
                .Setup(x => x.AddForwardingEventForSync(It.Is<CorrespondenceForwardingEventEntity>(f => f.ForwardedOnDate == fwdEvent02Date), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Guid.Empty); // Simulate duplicate event
            _forwardingEventRepositoryMock
                .Setup(x => x.AddForwardingEventForSync(It.Is<CorrespondenceForwardingEventEntity>(f => f.ForwardedOnDate == fwdEvent03Date), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Guid.Empty); // Simulate duplicate event

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceByIdForSync(correspondenceId, CorrespondenceSyncType.ForwardingEvents, It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();

            _forwardingEventRepositoryMock.VerifyNoOtherCalls();

            _backgroundJobClientMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Process_DuplicateForwardingEvents_RealWorld_NoneAdded()
        {
            // Arrange            
            var partyUuid = Guid.NewGuid();

            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)
                .WithAltinn2CorrespondenceId(12345)
                .WithIsMigrating(true) // Not available in Altinn 3 APIs
                .WithForwardingEvents(new List<CorrespondenceForwardingEventEntity>() {
                    new CorrespondenceForwardingEventEntity
                    {
                        // Example of Copy sent to own email address                        
                        ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 11, 0, 0)),
                        ForwardedByPartyUuid = partyUuid,
                        ForwardedByUserId = 123,
                        ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                        ForwardedToEmailAddress = "user1@awesometestusers.com",
                        ForwardingText = "Keep this as a backup in my email."
                    },
                    new CorrespondenceForwardingEventEntity
                    {
                        // Example of Copy sent to own email address wihtout text
                        ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 7, 11, 0, 0)),
                        ForwardedByPartyUuid = partyUuid,
                        ForwardedByUserId = 123,
                        ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                        ForwardedToEmailAddress = "user1@awesometestusers.com",
                    },
                    new CorrespondenceForwardingEventEntity
                    {
                        // Example of Instance Delegation by User 1 to User2
                        ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 12, 15, 0)),
                        ForwardedByPartyUuid = partyUuid,
                        ForwardedByUserId = 123,
                        ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                        ForwardedToUserId = 456,
                        ForwardedToUserUuid = new Guid("1D5FD16E-2905-414A-AC97-844929975F17"),
                        ForwardingText = "User2, - look into this for me please. - User1.",
                        ForwardedToEmailAddress  = "user2@awesometestusers.com"
                    }
                })
                .Build();
            var correspondenceId = correspondence.Id;

            var request = new SyncCorrespondenceForwardingEventRequest
            {
                CorrespondenceId = correspondenceId,
                SyncedEvents = new List<CorrespondenceForwardingEventEntity>
                {
                    new CorrespondenceForwardingEventEntity
                    {
                        // Example of Copy sent to own email address
                        ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 11, 0, 0)),
                        ForwardedByPartyUuid = partyUuid,
                        ForwardedByUserId = 123,
                        ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                        ForwardedToEmailAddress = "user1@awesometestusers.com",
                        ForwardingText = "Keep this as a backup in my email."
                    },
                    new CorrespondenceForwardingEventEntity
                    {
                        // Example of Copy sent to own email address wihtout text
                        ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 7, 11, 0, 0)),
                        ForwardedByPartyUuid = partyUuid,
                        ForwardedByUserId = 123,
                        ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                        ForwardedToEmailAddress = "user1@awesometestusers.com",
                    },
                    new CorrespondenceForwardingEventEntity
                    {
                        // Example of Instance Delegation by User 1 to User2
                        ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 12, 15, 0)),
                        ForwardedByPartyUuid = partyUuid,
                        ForwardedByUserId = 123,
                        ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                        ForwardedToUserId = 456,
                        ForwardedToUserUuid = new Guid("1D5FD16E-2905-414A-AC97-844929975F17"),
                        ForwardingText = "User2, - look into this for me please. - User1.",
                        ForwardedToEmailAddress  = "user2@awesometestusers.com"
                    }
                }
            };

            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceByIdForSync(correspondenceId, It.IsAny<CorrespondenceSyncType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(correspondence);

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceByIdForSync(correspondenceId, CorrespondenceSyncType.ForwardingEvents, It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();
            _forwardingEventRepositoryMock.VerifyNoOtherCalls();

            _backgroundJobClientMock.VerifyNoOtherCalls();
        }
    }
}