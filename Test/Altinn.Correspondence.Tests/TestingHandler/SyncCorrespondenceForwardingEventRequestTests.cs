using Altinn.Correspondence.Application.SyncCorrespondenceEvent;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Tests.Factories;
using Microsoft.Extensions.Logging;
using Moq;

namespace Altinn.Correspondence.Tests.TestingHandler
{
    public class SyncCorrespondenceForwardingEventRequestTests
    {
        private readonly Mock<ICorrespondenceRepository> _correspondenceRepositoryMock;
        private readonly Mock<ICorrespondenceForwardingEventRepository> _forwardingEventRepositoryMock;
        private readonly Mock<ILogger<SyncCorrespondenceForwardingEventHandler>> _loggerMock;
        private readonly SyncCorrespondenceForwardingEventHandler _handler;

        public SyncCorrespondenceForwardingEventRequestTests()
        {
            _correspondenceRepositoryMock = new Mock<ICorrespondenceRepository>();
            _forwardingEventRepositoryMock = new Mock<ICorrespondenceForwardingEventRepository>();
            _loggerMock = new Mock<ILogger<SyncCorrespondenceForwardingEventHandler>>();

            _handler = new SyncCorrespondenceForwardingEventHandler(
                _correspondenceRepositoryMock.Object,
                _forwardingEventRepositoryMock.Object,
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
                        // Example of Copy sendt to own email address
                        ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 11, 0, 0)),
                        ForwardedByPartyUuid = partyUuid,
                        ForwardedByUserId = 123,
                        ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                        ForwardedToEmailAddress = "user1@awesometestusers.com",
                        ForwardingText = "Keep this as a backup in my email."
                    },
                    new CorrespondenceForwardingEventEntity
                    {
                        // Example of Copy sendt to own digital mailbox
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
                .Setup(x => x.GetCorrespondenceById(correspondenceId, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ReturnsAsync(correspondence);
            // Mock forwarding event repository
            _forwardingEventRepositoryMock
                .Setup(x => x.AddForwardingEvents(It.IsAny<List<CorrespondenceForwardingEventEntity>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((List<CorrespondenceForwardingEventEntity> events, CancellationToken _) => events);

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceById(correspondenceId, false, false, true, It.IsAny<CancellationToken>(), true), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();

           _forwardingEventRepositoryMock.Verify(
                x => x.AddForwardingEvents(
                    It.Is<List<CorrespondenceForwardingEventEntity>>(n => n.Count == 3),
                    It.IsAny<CancellationToken>()),
                Times.Once
            );
            _forwardingEventRepositoryMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Process_DuplicateForwardingEvents_NoneAdded()
        {
            // Arrange            
            var partyUuid = Guid.NewGuid();

            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)
                .WithAltinn2CorrespondenceId(12345)
                .WithIsMigrating(true) // Not available in Altinn 3 APIs
                .WithForwardingEvents(new List<CorrespondenceForwardingEventEntity>() { new CorrespondenceForwardingEventEntity
                    {
                        // Example of Copy sendt to own email address
                        ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 11, 0, 0)),
                        ForwardedByPartyUuid = partyUuid,
                        ForwardedByUserId = 123,
                        ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                        ForwardedToEmailAddress = "user1@awesometestusers.com",
                        ForwardingText = "Keep this as a backup in my email."
                    },
                    new CorrespondenceForwardingEventEntity
                    {
                        // Example of Copy sendt to own digital mailbox
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
                        // Example of Copy sendt to own email address
                        ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 11, 0, 0)),
                        ForwardedByPartyUuid = partyUuid,
                        ForwardedByUserId = 123,
                        ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                        ForwardedToEmailAddress = "user1@awesometestusers.com",
                        ForwardingText = "Keep this as a backup in my email."
                    },
                    new CorrespondenceForwardingEventEntity
                    {
                        // Example of Copy sendt to own digital mailbox
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
                .Setup(x => x.GetCorrespondenceById(correspondenceId, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ReturnsAsync(correspondence);

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceById(correspondenceId, false, false, true, It.IsAny<CancellationToken>(), true), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();
            _forwardingEventRepositoryMock.VerifyNoOtherCalls();
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
                        // Example of Copy sendt to own email address                        
                        ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 11, 0, 0)),
                        ForwardedByPartyUuid = partyUuid,
                        ForwardedByUserId = 123,
                        ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                        ForwardedToEmailAddress = "user1@awesometestusers.com",
                        ForwardingText = "Keep this as a backup in my email."
                    },
                    new CorrespondenceForwardingEventEntity
                    {
                        // Example of Copy sendt to own email address wihtout text
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
                        // Example of Copy sendt to own email address
                        ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 11, 0, 0)),
                        ForwardedByPartyUuid = partyUuid,
                        ForwardedByUserId = 123,
                        ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                        ForwardedToEmailAddress = "user1@awesometestusers.com",
                        ForwardingText = "Keep this as a backup in my email."
                    },
                    new CorrespondenceForwardingEventEntity
                    {
                        // Example of Copy sendt to own email address wihtout text
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
                .Setup(x => x.GetCorrespondenceById(correspondenceId, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ReturnsAsync(correspondence);

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceById(correspondenceId, false, false, true, It.IsAny<CancellationToken>(), true), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();
            _forwardingEventRepositoryMock.VerifyNoOtherCalls();
        }
    }
} 