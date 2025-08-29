using Altinn.Correspondence.Application.PurgeCorrespondence;
using Altinn.Correspondence.Application.SyncCorrespondenceEvent;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Tests.Factories;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Altinn.Correspondence.Tests.TestingHandler
{
    public class SyncCorrespondenceStatusEventHandlerTests
    {
        private readonly Mock<ICorrespondenceRepository> _correspondenceRepositoryMock;
        private readonly Mock<ICorrespondenceStatusRepository> _correspondenceStatusRepositoryMock;
        private readonly Mock<ICorrespondenceDeleteEventRepository> _correspondenceDeleteEventRepositoryMock;
        private readonly Mock<IBackgroundJobClient> _backgroundJobClientMock;
        private readonly Mock<IAttachmentRepository> _attachmentRepositoryMock;
        private readonly Mock<IAttachmentStatusRepository> _attachmentStatusRepositoryMock;
        private readonly Mock<IStorageRepository> _storageRepositoryMock;
        private readonly Mock<IDialogportenService> _dialogPortenServiceMock;
        private readonly Mock<IAltinnRegisterService> _altinnRegisterServiceMock;
        private readonly Mock<ILogger<SyncCorrespondenceStatusEventHandler>> _loggerMock;
        private readonly SyncCorrespondenceStatusEventHandler _handler;
        private readonly PurgeCorrespondenceHelper _purgeHelper;

        public SyncCorrespondenceStatusEventHandlerTests()
        {
            _correspondenceRepositoryMock = new Mock<ICorrespondenceRepository>();
            _correspondenceStatusRepositoryMock = new Mock<ICorrespondenceStatusRepository>();
            _correspondenceDeleteEventRepositoryMock = new Mock<ICorrespondenceDeleteEventRepository>();
            _backgroundJobClientMock = new Mock<IBackgroundJobClient>();
            _attachmentRepositoryMock = new Mock<IAttachmentRepository>();
            _attachmentStatusRepositoryMock = new Mock<IAttachmentStatusRepository>();
            _storageRepositoryMock = new Mock<IStorageRepository>();
            _altinnRegisterServiceMock = new Mock<IAltinnRegisterService>();
            _dialogPortenServiceMock = new Mock<IDialogportenService>();
            _backgroundJobClientMock = new Mock<IBackgroundJobClient>();
            _purgeHelper = new PurgeCorrespondenceHelper(
                _attachmentRepositoryMock.Object,
                _storageRepositoryMock.Object,
                _attachmentStatusRepositoryMock.Object,
                _correspondenceRepositoryMock.Object,
                _correspondenceStatusRepositoryMock.Object,
                _dialogPortenServiceMock.Object,
                _backgroundJobClientMock.Object);
            _loggerMock = new Mock<ILogger<SyncCorrespondenceStatusEventHandler>>();

            _handler = new SyncCorrespondenceStatusEventHandler(
                _correspondenceRepositoryMock.Object,
                _correspondenceStatusRepositoryMock.Object,
                _correspondenceDeleteEventRepositoryMock.Object,
                _altinnRegisterServiceMock.Object,
                _purgeHelper,
                _backgroundJobClientMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task NotAvailable_ReadAndConfirmed_OK()
        {
            // Arrange            
            var partyUuid = Guid.NewGuid();

            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)                
                .WithAltinn2CorrespondenceId(12345)
                .WithIsMigrating(true) // Not available in Altinn 3 APIs
                .Build();
            var correspondenceId = correspondence.Id;

            var request = new SyncCorrespondenceStatusEventRequest
            {
                CorrespondenceId = correspondenceId,
                SyncedEvents = new List<CorrespondenceStatusEntity>
                {
                    new CorrespondenceStatusEntity
                    {
                        Status = CorrespondenceStatus.Read,
                        StatusChanged = DateTimeOffset.UtcNow,
                        PartyUuid = partyUuid
                    },
                    new CorrespondenceStatusEntity
                    {
                        Status = CorrespondenceStatus.Confirmed,
                        StatusChanged = DateTimeOffset.UtcNow,
                        PartyUuid = partyUuid
                    }
                }
            };

            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceById(correspondenceId,true,false,false, It.IsAny<CancellationToken>(), true))
                .ReturnsAsync(correspondence);

            _correspondenceStatusRepositoryMock
                .Setup(x => x.AddCorrespondenceStatuses(It.IsAny<List<CorrespondenceStatusEntity>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((List<CorrespondenceStatusEntity> r, CancellationToken _) => r);

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), true), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();

            // Verify that the statuses were added to Repository with SyncedFromAltinn2 set
            _correspondenceStatusRepositoryMock.Verify(
                x => x.AddCorrespondenceStatuses(It.Is<List<CorrespondenceStatusEntity>>(list =>
                    list.Count == 2 &&
                    list.Any(e => e.Status == CorrespondenceStatus.Read && e.PartyUuid == partyUuid && e.SyncedFromAltinn2 != null) &&
                    list.Any(e => e.Status == CorrespondenceStatus.Confirmed && e.PartyUuid == partyUuid && e.SyncedFromAltinn2 != null)
                ), It.IsAny<CancellationToken>()),
                Times.Once);
            _correspondenceStatusRepositoryMock.VerifyNoOtherCalls();

            // Should not trigger any Dialogporten changes or background jobs
            _backgroundJobClientMock.VerifyNoOtherCalls();
            _dialogPortenServiceMock.VerifyNoOtherCalls();
            _correspondenceDeleteEventRepositoryMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task NotAvailable_InvalidStatuses_NoUpdate()
        {
            // Arrange            
            var partyUuid = Guid.NewGuid();

            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)
                .WithAltinn2CorrespondenceId(12345)
                .WithIsMigrating(true) // Not available in Altinn 3 APIs
                .Build();
            var correspondenceId = correspondence.Id;

            var request = new SyncCorrespondenceStatusEventRequest
            {
                CorrespondenceId = correspondenceId,
                SyncedEvents = new List<CorrespondenceStatusEntity>
                {
                    new CorrespondenceStatusEntity
                    {
                        Status = CorrespondenceStatus.Published,
                        StatusChanged = DateTimeOffset.UtcNow,
                        PartyUuid = partyUuid
                    },
                    new CorrespondenceStatusEntity
                    {
                        Status = CorrespondenceStatus.Fetched,
                        StatusChanged = DateTimeOffset.UtcNow,
                        PartyUuid = partyUuid
                    },
                    new CorrespondenceStatusEntity
                    {
                        Status = CorrespondenceStatus.Replied,
                        StatusChanged = DateTimeOffset.UtcNow,
                        PartyUuid = partyUuid
                    }
                }
            };

            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), true))
                .ReturnsAsync(correspondence);

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), true), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();

            // Should not trigger any Dialogporten changes or background jobs
            _correspondenceStatusRepositoryMock.VerifyNoOtherCalls();
            _backgroundJobClientMock.VerifyNoOtherCalls();
            _dialogPortenServiceMock.VerifyNoOtherCalls();
            _correspondenceDeleteEventRepositoryMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task NotAvailable_SecondReadSameTime_HandledAsDuplicate()
        {
            // Arrange            
            var partyUuid = Guid.NewGuid();

            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)
                .WithStatus(CorrespondenceStatus.Read, new DateTime(2023, 10, 1, 12, 0, 0), partyUuid)
                .WithAltinn2CorrespondenceId(12345)
                .WithIsMigrating(true) // Not available in Altinn 3 APIs
                .Build();
            var correspondenceId = correspondence.Id;

            var request = new SyncCorrespondenceStatusEventRequest
            {
                CorrespondenceId = correspondenceId,
                SyncedEvents = new List<CorrespondenceStatusEntity>
                {
                    new CorrespondenceStatusEntity
                    {
                        Status = CorrespondenceStatus.Read,
                        StatusChanged =  new DateTime(2023, 10, 1, 12, 0, 0),
                        PartyUuid = partyUuid
                    }
                }
            };

            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), true))
                .ReturnsAsync(correspondence);
            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), true), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();

            // Should not trigger any Dialogporten changes or background jobs
            _correspondenceStatusRepositoryMock.VerifyNoOtherCalls();
            _backgroundJobClientMock.VerifyNoOtherCalls();
            _dialogPortenServiceMock.VerifyNoOtherCalls();
            _correspondenceDeleteEventRepositoryMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task NotAvailable_MultipleDuplicatesInRequest_OnlyOneOfEachSaved()
        {
            // Arrange            
            var partyUuid = Guid.NewGuid();

            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)
                .WithStatus(CorrespondenceStatus.Read, new DateTime(2023, 10, 1, 11, 0, 0, 0), partyUuid)                
                .WithAltinn2CorrespondenceId(12345)
                .WithIsMigrating(true) // Not available in Altinn 3 APIs
                .Build();
            var correspondenceId = correspondence.Id;

            var request = new SyncCorrespondenceStatusEventRequest
            {
                CorrespondenceId = correspondenceId,
                SyncedEvents = new List<CorrespondenceStatusEntity>
                {
                    new CorrespondenceStatusEntity
                    {
                        Status = CorrespondenceStatus.Read,
                        StatusChanged =  new DateTime(2023, 10, 1, 12, 0, 0, 0),
                        PartyUuid = partyUuid
                    },
                    new CorrespondenceStatusEntity
                    {
                        Status = CorrespondenceStatus.Read,
                        StatusChanged =  new DateTime(2023, 10, 1, 12, 0, 0, 150),
                        PartyUuid = partyUuid
                    },
                    new CorrespondenceStatusEntity
                    {
                        Status = CorrespondenceStatus.Confirmed,
                        StatusChanged =  new DateTime(2023, 10, 1, 12, 5, 0, 0),
                        PartyUuid = partyUuid
                    },
                    new CorrespondenceStatusEntity
                    {
                        Status = CorrespondenceStatus.Confirmed,
                        StatusChanged =  new DateTime(2023, 10, 1, 12, 5, 0, 150),
                        PartyUuid = partyUuid
                    },
                    new CorrespondenceStatusEntity
                    {
                        Status = CorrespondenceStatus.Archived,
                        StatusChanged =  new DateTime(2023, 10, 1, 12, 6, 0, 150),
                        PartyUuid = partyUuid
                    },
                    new CorrespondenceStatusEntity
                    {
                        Status = CorrespondenceStatus.Archived,
                        StatusChanged =  new DateTime(2023, 10, 1, 12, 6, 0, 150),
                        PartyUuid = partyUuid
                    }
                },
                SyncedDeleteEvents = new List<CorrespondenceDeleteEventEntity>
                {
                    new CorrespondenceDeleteEventEntity
                    {
                        EventType = CorrespondenceDeleteEventType.HardDeletedByRecipient,
                        EventOccurred = new DateTime(2023, 10, 1, 12, 7, 0),
                        PartyUuid = partyUuid
                    },
                    new CorrespondenceDeleteEventEntity
                    {
                        EventType = CorrespondenceDeleteEventType.HardDeletedByRecipient,
                        EventOccurred = new DateTime(2023, 10, 1, 12, 7, 0, 500),
                        PartyUuid = partyUuid
                    }
                }
            };

            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), true))
                .ReturnsAsync(correspondence);
            _attachmentRepositoryMock
                .Setup(x => x.GetAttachmentsByCorrespondence(correspondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<AttachmentEntity>());
            _correspondenceDeleteEventRepositoryMock
                .Setup(x => x.GetDeleteEventsForCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CorrespondenceDeleteEventEntity>());

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), true), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();

            _correspondenceStatusRepositoryMock.Verify(
               x => x.AddCorrespondenceStatuses(It.Is<List<CorrespondenceStatusEntity>>(list =>
                   list.Count == 3 &&
                   list.Any(e => e.Status == CorrespondenceStatus.Read && e.PartyUuid == partyUuid && e.SyncedFromAltinn2 != null) &&
                   list.Any(e => e.Status == CorrespondenceStatus.Confirmed && e.PartyUuid == partyUuid && e.SyncedFromAltinn2 != null) &&
                   list.Any(e => e.Status == CorrespondenceStatus.Archived && e.PartyUuid == partyUuid && e.SyncedFromAltinn2 != null)
               ), It.IsAny<CancellationToken>()),
               Times.Once);
            _correspondenceStatusRepositoryMock.Verify(x => x.AddCorrespondenceStatus(
                It.Is<CorrespondenceStatusEntity>(e =>
                    e.CorrespondenceId == correspondenceId &&
                    e.Status == CorrespondenceStatus.PurgedByRecipient &&
                    e.StatusChanged.Equals(new DateTimeOffset(new DateTime(2023, 10, 1, 12, 7, 0))) &&
                    e.PartyUuid == partyUuid &&
                    e.SyncedFromAltinn2 != null),
                It.IsAny<CancellationToken>()),
                Times.Once);
            _correspondenceStatusRepositoryMock.VerifyNoOtherCalls();

            // Should not trigger any Dialogporten changes or background jobs
            _backgroundJobClientMock.VerifyNoOtherCalls();
            _dialogPortenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task NotAvailable_SecondRead2SecondsLater_Updated()
        {
            // Arrange            
            var partyUuid = Guid.NewGuid();

            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)
                .WithStatus(CorrespondenceStatus.Read, new DateTime(2023, 10, 1, 12, 0, 0), partyUuid)
                .WithAltinn2CorrespondenceId(12345)
                .WithIsMigrating(true) // Not available in Altinn 3 APIs
                .Build();
            var correspondenceId = correspondence.Id;

            var request = new SyncCorrespondenceStatusEventRequest
            {
                CorrespondenceId = correspondenceId,
                SyncedEvents = new List<CorrespondenceStatusEntity>
                {
                    new CorrespondenceStatusEntity
                    {
                        Status = CorrespondenceStatus.Read,
                        StatusChanged =  new DateTime(2023, 10, 1, 12, 0, 2),
                        PartyUuid = partyUuid
                    }
                }
            };

            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), true))
                .ReturnsAsync(correspondence);

            _correspondenceStatusRepositoryMock
                .Setup(x => x.AddCorrespondenceStatuses(It.IsAny<List<CorrespondenceStatusEntity>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((List<CorrespondenceStatusEntity> r, CancellationToken _) => r);

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), true), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();

            // Verify that the status were added to Repository with SyncedFromAltinn2 set
            _correspondenceStatusRepositoryMock.Verify(
                x => x.AddCorrespondenceStatuses(It.Is<List<CorrespondenceStatusEntity>>(list =>
                    list.Count == 1 &&
                    list.Any(e => e.Status == CorrespondenceStatus.Read && e.PartyUuid == partyUuid && e.SyncedFromAltinn2 != null)
                ), It.IsAny<CancellationToken>()),
                Times.Once);
            _correspondenceStatusRepositoryMock.VerifyNoOtherCalls();

            // Should not trigger any Dialogporten changes or background jobs
            _backgroundJobClientMock.VerifyNoOtherCalls();
            _dialogPortenServiceMock.VerifyNoOtherCalls();
            _correspondenceDeleteEventRepositoryMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Available_ReadAndConfirmed_OK()
        {
            // Arrange
            var partyUuid = Guid.NewGuid();

            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)
                .WithAltinn2CorrespondenceId(12345)
                .WithDialogId("dialog-id-123")
                .Build();
            var correspondenceId = correspondence.Id;
            var recipient = correspondence.Recipient;
            var request = new SyncCorrespondenceStatusEventRequest
            {
                CorrespondenceId = correspondenceId,
                SyncedEvents = new List<CorrespondenceStatusEntity>
                {
                    new CorrespondenceStatusEntity
                    {
                        Status = CorrespondenceStatus.Read,
                        StatusChanged = DateTimeOffset.UtcNow,
                        PartyUuid = partyUuid
                    },
                    new CorrespondenceStatusEntity
                    {
                        Status = CorrespondenceStatus.Confirmed,
                        StatusChanged = DateTimeOffset.UtcNow,
                        PartyUuid = partyUuid
                    }
                }
            };

            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), true))
                .ReturnsAsync(correspondence);
            _correspondenceStatusRepositoryMock
                .Setup(x => x.AddCorrespondenceStatuses(It.IsAny<List<CorrespondenceStatusEntity>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((List<CorrespondenceStatusEntity> r, CancellationToken _) => r);   
            

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), true), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();

            // Verify that the statuses were added to Repository with SyncedFromAltinn2 set
            _correspondenceStatusRepositoryMock.Verify(
                x => x.AddCorrespondenceStatuses(It.Is<List<CorrespondenceStatusEntity>>(list =>
                    list.Count == 2 &&
                    list.Any(e => e.Status == CorrespondenceStatus.Read && e.PartyUuid == partyUuid && e.SyncedFromAltinn2 != null) &&
                    list.Any(e => e.Status == CorrespondenceStatus.Confirmed && e.PartyUuid == partyUuid && e.SyncedFromAltinn2 != null)
                ), It.IsAny<CancellationToken>()),
                Times.Once);
            _correspondenceStatusRepositoryMock.VerifyNoOtherCalls();

            // Verify background job for Altinn Events
            VerifyAltinnEventEnqueued(correspondenceId, AltinnEventType.CorrespondenceReceiverRead, recipient);
            VerifyAltinnEventEnqueued(correspondenceId, AltinnEventType.CorrespondenceReceiverConfirmed, recipient);
            // Verify background jobs Dialogporten activities
            VerifyDialogportenServiceCreateInformationActivityEnqueued(correspondenceId, DialogportenActorType.Recipient, DialogportenTextType.CorrespondenceConfirmed, recipient);
            VerifyDialogportenServicePatchCorrespondenceDialogToConfirmedEnqueued(correspondenceId);
            VerifyDialogportenServiceCreateOpenedActivityEnqueued(correspondenceId);
          
            // Should not trigger any additional Dialogporten changes or background jobs
            _backgroundJobClientMock.VerifyNoOtherCalls();
            _dialogPortenServiceMock.VerifyNoOtherCalls();
            _correspondenceDeleteEventRepositoryMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Available_Archived_OK()
        {
            // Arrange
            var partyUuid = Guid.NewGuid();
            var partyOrgnumber = "123456789";
            var partyIdentifier = $"{UrnConstants.OrganizationNumberAttribute}:{partyOrgnumber}";

            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)
                .WithAltinn2CorrespondenceId(12345)
                .WithDialogId("dialog-id-123")
                .Build();
            var correspondenceId = correspondence.Id;
            var recipient = correspondence.Recipient;
            var request = new SyncCorrespondenceStatusEventRequest
            {
                CorrespondenceId = correspondenceId,
                SyncedEvents = new List<CorrespondenceStatusEntity>
                {
                    new CorrespondenceStatusEntity
                    {
                        Status = CorrespondenceStatus.Archived,
                        StatusChanged = DateTimeOffset.UtcNow,
                        PartyUuid = partyUuid
                    }
                }
            };

            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), true))
                .ReturnsAsync(correspondence);
            _correspondenceStatusRepositoryMock
                .Setup(x => x.AddCorrespondenceStatuses(It.IsAny<List<CorrespondenceStatusEntity>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((List<CorrespondenceStatusEntity> r, CancellationToken _) => r);           
            _altinnRegisterServiceMock
                .Setup(x => x.LookUpPartyByPartyUuid(partyUuid, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Core.Models.Entities.Party { PartyUuid = partyUuid, OrgNumber = partyOrgnumber, PartyTypeName = PartyType.Organization });

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), true), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();

            // Verify that the statuses were added to Repository with SyncedFromAltinn2 set
            _correspondenceStatusRepositoryMock.Verify(
                x => x.AddCorrespondenceStatuses(It.Is<List<CorrespondenceStatusEntity>>(list =>
                    list.Count == 1 &&
                    list.Any(e => e.Status == CorrespondenceStatus.Archived && e.PartyUuid == partyUuid && e.SyncedFromAltinn2 != null)
                ), It.IsAny<CancellationToken>()),
                Times.Once);
            _correspondenceStatusRepositoryMock.VerifyNoOtherCalls();
            
            // Verify background jobs Dialogporten activities            
            VerifyDialogportenServiceSetArchivedSystemLabelOnDialogEnqueued(correspondenceId, partyIdentifier);

            // Verify register lookup performed
            _altinnRegisterServiceMock.Verify(_altinnRegisterServiceMock => _altinnRegisterServiceMock.LookUpPartyByPartyUuid(partyUuid, It.IsAny<CancellationToken>()), Times.Once);
            _altinnRegisterServiceMock.VerifyNoOtherCalls();

            // Should not trigger any additional Dialogporten changes or background jobs
            _backgroundJobClientMock.VerifyNoOtherCalls();
            _dialogPortenServiceMock.VerifyNoOtherCalls();
            _correspondenceDeleteEventRepositoryMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Available_PurgedByRecipient_OK()
        {
            // Arrange
            var partyUuid = Guid.NewGuid();

            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)
                .WithAltinn2CorrespondenceId(12345)
                .WithDialogId("dialog-id-123")
                .Build();
            var correspondenceId = correspondence.Id;
            var recipient = correspondence.Recipient;
            var sender = correspondence.Sender;
            var request = new SyncCorrespondenceStatusEventRequest
            {
                CorrespondenceId = correspondenceId,
                SyncedEvents = null,
                SyncedDeleteEvents = new List<CorrespondenceDeleteEventEntity>
                {
                    new CorrespondenceDeleteEventEntity
                    {
                        EventType = CorrespondenceDeleteEventType.HardDeletedByRecipient,
                        EventOccurred = new  DateTimeOffset(new DateTime(2025, 8, 1, 12, 0, 0)),
                        PartyUuid = partyUuid
                    }
                }
            };

            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), true))
                .ReturnsAsync(correspondence);
            _correspondenceStatusRepositoryMock
                .Setup(x => x.AddCorrespondenceStatuses(It.IsAny<List<CorrespondenceStatusEntity>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((List<CorrespondenceStatusEntity> r, CancellationToken _) => r);
            _correspondenceDeleteEventRepositoryMock
                .Setup(x => x.GetDeleteEventsForCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CorrespondenceDeleteEventEntity>());
            _correspondenceDeleteEventRepositoryMock
               .Setup(x => x.AddDeleteEvent(It.IsAny<CorrespondenceDeleteEventEntity>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((CorrespondenceDeleteEventEntity r, CancellationToken _) => r);
            _attachmentRepositoryMock
                .Setup(x => x.GetAttachmentsByCorrespondence(correspondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<AttachmentEntity>());

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), true), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();

            // Verify that the statuses were added to Repository with SyncedFromAltinn2 set
            _correspondenceStatusRepositoryMock.Verify(x => x.AddCorrespondenceStatus(
                It.Is<CorrespondenceStatusEntity>(e =>
                    e.CorrespondenceId == correspondenceId &&
                    e.Status == CorrespondenceStatus.PurgedByRecipient &&
                    e.StatusChanged.Equals(new DateTimeOffset(new DateTime(2025, 8, 1, 12, 0, 0))) &&
                    e.PartyUuid == partyUuid &&
                    e.SyncedFromAltinn2 != null),
                It.IsAny<CancellationToken>()),
                Times.Once);
            _correspondenceStatusRepositoryMock.VerifyNoOtherCalls();

            _correspondenceDeleteEventRepositoryMock.Verify(x => x.AddDeleteEvent(
               It.Is<CorrespondenceDeleteEventEntity>(e =>
                   e.EventType == CorrespondenceDeleteEventType.HardDeletedByRecipient
                   && e.PartyUuid == partyUuid
                   && e.EventOccurred.Equals(new DateTimeOffset(new DateTime(2025, 8, 1, 12, 0, 0)))
                   && e.SyncedFromAltinn2 != null),
               It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceDeleteEventRepositoryMock.Verify(x => x.GetDeleteEventsForCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceDeleteEventRepositoryMock.VerifyNoOtherCalls();
            
            // Verify background job for Altinn Events
            VerifyAltinnEventEnqueued(correspondenceId, AltinnEventType.CorrespondencePurged, sender);
            // Verify background jobs Dialogporten activities
            VerifyDialogportenServiceCreatePurgedActivityEnqueued(correspondenceId,DialogportenActorType.Recipient, "mottaker", new DateTimeOffset(new DateTime(2025, 8, 1, 12, 0, 0)));
            VerifyDialogportenServiceSoftDeleteDialogEnqueued("dialog-id-123");
            // Should not trigger any additional Dialogporten changes or background jobs
            _backgroundJobClientMock.VerifyNoOtherCalls();
            _dialogPortenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Available_SoftDeleteByRecipient_OK()
        {
            // Arrange
            var partyUuid = Guid.NewGuid();
            var partyOrgnumber = "123456789";
            var partyIdentifier = $"{UrnConstants.OrganizationNumberAttribute}:{partyOrgnumber}";

            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)
                .WithAltinn2CorrespondenceId(12345)
                .WithDialogId("dialog-id-123")
                .Build();
            var correspondenceId = correspondence.Id;
            var recipient = correspondence.Recipient;
            var sender = correspondence.Sender;
            var request = new SyncCorrespondenceStatusEventRequest
            {
                CorrespondenceId = correspondenceId,
                SyncedEvents = null,
                SyncedDeleteEvents = new List<CorrespondenceDeleteEventEntity>
                {
                    new CorrespondenceDeleteEventEntity
                    {
                        EventType = CorrespondenceDeleteEventType.SoftDeletedByRecipient,
                        EventOccurred = new  DateTimeOffset(new DateTime(2025, 8, 1, 12, 0, 0)),
                        PartyUuid = partyUuid
                    }
                }
            };

            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), true))
                .ReturnsAsync(correspondence);
            _correspondenceStatusRepositoryMock
                .Setup(x => x.AddCorrespondenceStatuses(It.IsAny<List<CorrespondenceStatusEntity>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((List<CorrespondenceStatusEntity> r, CancellationToken _) => r);
            _correspondenceDeleteEventRepositoryMock
                .Setup(x => x.GetDeleteEventsForCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CorrespondenceDeleteEventEntity>());
            _correspondenceDeleteEventRepositoryMock
               .Setup(x => x.AddDeleteEvent(It.IsAny<CorrespondenceDeleteEventEntity>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((CorrespondenceDeleteEventEntity r, CancellationToken _) => r);
            _attachmentRepositoryMock
                .Setup(x => x.GetAttachmentsByCorrespondence(correspondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<AttachmentEntity>());
            _altinnRegisterServiceMock
                .Setup(x => x.LookUpPartyByPartyUuid(partyUuid, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Party { PartyUuid = partyUuid, OrgNumber = partyOrgnumber, PartyTypeName = PartyType.Organization });

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), true), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();

            // Verify that no statuses were added to Repository with SyncedFromAltinn2 set
            _correspondenceStatusRepositoryMock.VerifyNoOtherCalls();

            _correspondenceDeleteEventRepositoryMock.Verify(x => x.AddDeleteEvent(
               It.Is<CorrespondenceDeleteEventEntity>(e =>
                   e.EventType == CorrespondenceDeleteEventType.SoftDeletedByRecipient
                   && e.PartyUuid == partyUuid
                   && e.EventOccurred.Equals(new DateTimeOffset(new DateTime(2025, 8, 1, 12, 0, 0)))
                   && e.SyncedFromAltinn2 != null),
               It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceDeleteEventRepositoryMock.Verify(x => x.GetDeleteEventsForCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceDeleteEventRepositoryMock.VerifyNoOtherCalls();

            // Verify background jobs Dialogporten activities
            VerifySoftDeleteUpdateForDialogportenEnqueued(correspondence.Id, partyIdentifier, CorrespondenceDeleteEventType.SoftDeletedByRecipient);

            // Should not trigger any additional Dialogporten changes or background jobs
            _backgroundJobClientMock.VerifyNoOtherCalls();
            _dialogPortenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Available_SoftDeleteByRecipientHardDeleteByRecipient_OK()
        {
            // Arrange
            var partyUuid = Guid.NewGuid();
            var partyOrgnumber = "123456789";
            var partyIdentifier = $"{UrnConstants.OrganizationNumberAttribute}:{partyOrgnumber}";

            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)
                .WithAltinn2CorrespondenceId(12345)
                .WithDialogId("dialog-id-123")
                .Build();
            var correspondenceId = correspondence.Id;
            var recipient = correspondence.Recipient;
            var sender = correspondence.Sender;
            var request = new SyncCorrespondenceStatusEventRequest
            {
                CorrespondenceId = correspondenceId,
                SyncedEvents = null,
                SyncedDeleteEvents = new List<CorrespondenceDeleteEventEntity>
                {
                    new CorrespondenceDeleteEventEntity
                    {
                        EventType = CorrespondenceDeleteEventType.SoftDeletedByRecipient,
                        EventOccurred = new  DateTimeOffset(new DateTime(2025, 8, 1, 12, 0, 0)),
                        PartyUuid = partyUuid
                    },
                    new CorrespondenceDeleteEventEntity
                    {
                        EventType = CorrespondenceDeleteEventType.HardDeletedByRecipient,
                        EventOccurred = new  DateTimeOffset(new DateTime(2025, 8, 15, 12, 0, 0)),
                        PartyUuid = partyUuid
                    }
                }
            };

            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), true))
                .ReturnsAsync(correspondence);
            _correspondenceStatusRepositoryMock
                .Setup(x => x.AddCorrespondenceStatuses(It.IsAny<List<CorrespondenceStatusEntity>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((List<CorrespondenceStatusEntity> r, CancellationToken _) => r);
            _correspondenceDeleteEventRepositoryMock
                .Setup(x => x.GetDeleteEventsForCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CorrespondenceDeleteEventEntity>());
            _correspondenceDeleteEventRepositoryMock
               .Setup(x => x.AddDeleteEvent(It.IsAny<CorrespondenceDeleteEventEntity>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((CorrespondenceDeleteEventEntity r, CancellationToken _) => r);
            _attachmentRepositoryMock
                .Setup(x => x.GetAttachmentsByCorrespondence(correspondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<AttachmentEntity>());
            _altinnRegisterServiceMock
                .Setup(x => x.LookUpPartyByPartyUuid(partyUuid, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Party { PartyUuid = partyUuid, OrgNumber = partyOrgnumber, PartyTypeName = PartyType.Organization });

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), true), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();

            // Verify that Purge status was added to Repository with SyncedFromAltinn2 set
            _correspondenceStatusRepositoryMock.Verify(x => x.AddCorrespondenceStatus(
                It.Is<CorrespondenceStatusEntity>(e =>
                    e.CorrespondenceId == correspondenceId &&
                    e.Status == CorrespondenceStatus.PurgedByRecipient &&
                    e.StatusChanged.Equals(new DateTimeOffset(new DateTime(2025, 8, 15, 12, 0, 0))) &&
                    e.PartyUuid == partyUuid &&
                    e.SyncedFromAltinn2 != null),
                It.IsAny<CancellationToken>()),
                Times.Once);
            _correspondenceStatusRepositoryMock.VerifyNoOtherCalls();

            _correspondenceDeleteEventRepositoryMock.Verify(x => x.AddDeleteEvent(
               It.Is<CorrespondenceDeleteEventEntity>(e =>
                   e.EventType == CorrespondenceDeleteEventType.SoftDeletedByRecipient
                   && e.PartyUuid == partyUuid
                   && e.EventOccurred.Equals(new DateTimeOffset(new DateTime(2025, 8, 1, 12, 0, 0)))
                   && e.SyncedFromAltinn2 != null),
               It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceDeleteEventRepositoryMock.Verify(x => x.AddDeleteEvent(
               It.Is<CorrespondenceDeleteEventEntity>(e =>
                   e.EventType == CorrespondenceDeleteEventType.HardDeletedByRecipient
                   && e.PartyUuid == partyUuid
                   && e.EventOccurred.Equals(new DateTimeOffset(new DateTime(2025, 8, 15, 12, 0, 0)))
                   && e.SyncedFromAltinn2 != null),
               It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceDeleteEventRepositoryMock.Verify(x => x.GetDeleteEventsForCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()), Times.Once);            

            // Verify background job for Altinn Events
            VerifyAltinnEventEnqueued(correspondenceId, AltinnEventType.CorrespondencePurged, sender);

            // Verify background jobs Dialogporten activities
            VerifySoftDeleteUpdateForDialogportenEnqueued(correspondence.Id, partyIdentifier, CorrespondenceDeleteEventType.SoftDeletedByRecipient);
            VerifyDialogportenServiceCreatePurgedActivityEnqueued(correspondenceId, DialogportenActorType.Recipient, "mottaker", new DateTimeOffset(new DateTime(2025, 8, 15, 12, 0, 0)));

            // Should not trigger any additional Dialogporten changes or background jobs
            _correspondenceDeleteEventRepositoryMock.VerifyNoOtherCalls();
            _backgroundJobClientMock.VerifyNoOtherCalls();
            _dialogPortenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Available_RestoreByRecipient_OK()
        {
            // Arrange
            var partyUuid = Guid.NewGuid();
            var partyOrgnumber = "123456789";
            var partyIdentifier = $"{UrnConstants.OrganizationNumberAttribute}:{partyOrgnumber}";

            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)
                .WithAltinn2CorrespondenceId(12345)
                .WithDialogId("dialog-id-123")
                .Build();
            var correspondenceId = correspondence.Id;
            var recipient = correspondence.Recipient;
            var sender = correspondence.Sender;
            var request = new SyncCorrespondenceStatusEventRequest
            {
                CorrespondenceId = correspondenceId,
                SyncedEvents = null,
                SyncedDeleteEvents = new List<CorrespondenceDeleteEventEntity>
                {
                    new CorrespondenceDeleteEventEntity
                    {
                        EventType = CorrespondenceDeleteEventType.RestoredByRecipient,
                        EventOccurred = new  DateTimeOffset(new DateTime(2025, 8, 1, 12, 0, 0)),
                        PartyUuid = partyUuid
                    }
                }
            };

            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), true))
                .ReturnsAsync(correspondence);
            _correspondenceStatusRepositoryMock
                .Setup(x => x.AddCorrespondenceStatuses(It.IsAny<List<CorrespondenceStatusEntity>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((List<CorrespondenceStatusEntity> r, CancellationToken _) => r);
            _correspondenceDeleteEventRepositoryMock
                .Setup(x => x.GetDeleteEventsForCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CorrespondenceDeleteEventEntity>());
            _correspondenceDeleteEventRepositoryMock
               .Setup(x => x.AddDeleteEvent(It.IsAny<CorrespondenceDeleteEventEntity>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((CorrespondenceDeleteEventEntity r, CancellationToken _) => r);
            _attachmentRepositoryMock
                .Setup(x => x.GetAttachmentsByCorrespondence(correspondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<AttachmentEntity>());
            _altinnRegisterServiceMock
                .Setup(x => x.LookUpPartyByPartyUuid(partyUuid, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Party { PartyUuid = partyUuid, OrgNumber = partyOrgnumber, PartyTypeName = PartyType.Organization });

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), true), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();

            // Verify that no statuses were added to Repository with SyncedFromAltinn2 set
            _correspondenceStatusRepositoryMock.VerifyNoOtherCalls();

            _correspondenceDeleteEventRepositoryMock.Verify(x => x.AddDeleteEvent(
               It.Is<CorrespondenceDeleteEventEntity>(e =>
                   e.EventType == CorrespondenceDeleteEventType.RestoredByRecipient
                   && e.PartyUuid == partyUuid
                   && e.EventOccurred.Equals(new DateTimeOffset(new DateTime(2025, 8, 1, 12, 0, 0)))
                   && e.SyncedFromAltinn2 != null),
               It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceDeleteEventRepositoryMock.Verify(x => x.GetDeleteEventsForCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceDeleteEventRepositoryMock.VerifyNoOtherCalls();

            // Verify background jobs Dialogporten activities
            VerifySoftDeleteUpdateForDialogportenEnqueued(correspondence.Id, partyIdentifier, CorrespondenceDeleteEventType.RestoredByRecipient);

            // Should not trigger any additional Dialogporten changes or background jobs
            _backgroundJobClientMock.VerifyNoOtherCalls();
            _dialogPortenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Available_PurgedByAltinnWithAttachments_OK()
        {
            // Arrange
            var partyUuid = Guid.NewGuid();

            var correspondence = new CorrespondenceEntityBuilder()
                .WithCreated(new DateTime(2025, 8, 1, 12, 0, 0))
                .WithStatus(CorrespondenceStatus.Published, new DateTime(2025, 8, 1, 12, 0, 0), partyUuid)
                .WithDialogId("dialog-id-123")
                .WithAltinn2CorrespondenceId(12345)
                .WithAttachment("fjas.txt")
                .Build();
            var correspondenceId = correspondence.Id;
            var recipient = correspondence.Recipient;
            var sender = correspondence.Sender;
            var request = new SyncCorrespondenceStatusEventRequest
            {
                CorrespondenceId = correspondenceId,
                SyncedEvents = null,
                SyncedDeleteEvents = new List<CorrespondenceDeleteEventEntity>
                {
                    new CorrespondenceDeleteEventEntity
                    {
                        EventType = CorrespondenceDeleteEventType.HardDeletedByServiceOwner,
                        EventOccurred = new  DateTimeOffset(new DateTime(2025, 8, 1, 12, 0, 0)),
                        PartyUuid = partyUuid
                    }
                }
            };

            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), true))
                .ReturnsAsync(correspondence);
            _correspondenceStatusRepositoryMock
                .Setup(x => x.AddCorrespondenceStatuses(It.IsAny<List<CorrespondenceStatusEntity>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((List<CorrespondenceStatusEntity> r, CancellationToken _) => r);
            _correspondenceDeleteEventRepositoryMock
                .Setup(x => x.GetDeleteEventsForCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CorrespondenceDeleteEventEntity>());
            _correspondenceDeleteEventRepositoryMock
               .Setup(x => x.AddDeleteEvent(It.IsAny<CorrespondenceDeleteEventEntity>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((CorrespondenceDeleteEventEntity r, CancellationToken _) => r);
            _attachmentRepositoryMock
                .Setup(x => x.GetAttachmentsByCorrespondence(correspondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<AttachmentEntity>
                {
                    new AttachmentEntity
                    {
                        Id = Guid.NewGuid(),
                        ResourceId = correspondence.ResourceId,
                        FileName  = correspondence.Content.Attachments.First().Attachment.FileName,
                        SendersReference = correspondence.Content.Attachments.First().Attachment.SendersReference,
                        Sender = sender,
                        Created =  correspondence.Content.Attachments.First().Attachment.Created
                    }
                });
            
            _attachmentRepositoryMock
                .Setup(x => x.CanAttachmentBeDeleted(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _attachmentStatusRepositoryMock
                .Setup(x => x.AddAttachmentStatus(It.IsAny<AttachmentStatusEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Guid.NewGuid());

            _storageRepositoryMock
                .Setup(x => x.PurgeAttachment(It.IsAny<Guid>(), It.IsAny<StorageProviderEntity>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), true), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();

            // Verify that the statuses were added to Repository with SyncedFromAltinn2 set
            _correspondenceStatusRepositoryMock.Verify(x => x.AddCorrespondenceStatus(
                It.Is<CorrespondenceStatusEntity>(e =>
                    e.CorrespondenceId == correspondenceId &&
                    e.Status == CorrespondenceStatus.PurgedByAltinn &&
                    e.StatusChanged.Equals(new DateTimeOffset(new DateTime(2025, 8, 1, 12, 0, 0))) &&
                    e.PartyUuid == partyUuid &&
                    e.SyncedFromAltinn2 != null),
                It.IsAny<CancellationToken>()),
                Times.Once);
            _correspondenceStatusRepositoryMock.VerifyNoOtherCalls();

            _correspondenceDeleteEventRepositoryMock.Verify(x => x.AddDeleteEvent(
                It.Is<CorrespondenceDeleteEventEntity>(e =>
                    e.EventType == CorrespondenceDeleteEventType.HardDeletedByServiceOwner 
                    && e.PartyUuid == partyUuid 
                    && e.EventOccurred.Equals(new DateTimeOffset(new DateTime(2025, 8, 1, 12, 0, 0)))
                    && e.SyncedFromAltinn2 != null),                
                It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceDeleteEventRepositoryMock.Verify(x => x.GetDeleteEventsForCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceDeleteEventRepositoryMock.VerifyNoOtherCalls();

            // Verify background job for Altinn Events
            VerifyAltinnEventEnqueued(correspondenceId, AltinnEventType.CorrespondencePurged, sender);
            // Verify background jobs Dialogporten activities
            VerifyDialogportenServiceCreatePurgedActivityEnqueued(correspondenceId, DialogportenActorType.Sender, "avsender", new DateTimeOffset(new DateTime(2025, 8, 1, 12, 0, 0)));

            // Should not trigger any additional Dialogporten changes or background jobs
            _backgroundJobClientMock.VerifyNoOtherCalls();
            _dialogPortenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task NotAvailable_PurgedByRecipient_OK()
        {
            // Arrange
            var partyUuid = Guid.NewGuid();

            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)
                .WithAltinn2CorrespondenceId(12345)
                .WithIsMigrating(true) 
                .Build();
            var correspondenceId = correspondence.Id;
            var recipient = correspondence.Recipient;
            var sender = correspondence.Sender;
            var request = new SyncCorrespondenceStatusEventRequest
            {
                CorrespondenceId = correspondenceId,
                SyncedEvents = null,
                SyncedDeleteEvents = new List<CorrespondenceDeleteEventEntity>
                { 
                    new CorrespondenceDeleteEventEntity
                    {
                        EventType = CorrespondenceDeleteEventType.HardDeletedByRecipient,
                        EventOccurred = new DateTimeOffset(new DateTime(2025, 8, 1, 12, 0, 0)),
                        PartyUuid = partyUuid
                    }
                }
            };

            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), true))
                .ReturnsAsync(correspondence);
            _correspondenceStatusRepositoryMock
                .Setup(x => x.AddCorrespondenceStatuses(It.IsAny<List<CorrespondenceStatusEntity>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((List<CorrespondenceStatusEntity> r, CancellationToken _) => r);
            _correspondenceDeleteEventRepositoryMock
                .Setup(x => x.GetDeleteEventsForCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CorrespondenceDeleteEventEntity>());
            _correspondenceDeleteEventRepositoryMock
               .Setup(x => x.AddDeleteEvent(It.IsAny<CorrespondenceDeleteEventEntity>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((CorrespondenceDeleteEventEntity r, CancellationToken _) => r);
            _attachmentRepositoryMock
                .Setup(x => x.GetAttachmentsByCorrespondence(correspondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<AttachmentEntity>());

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), true), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();

            // Verify that the purged status was added to Repository with SyncedFromAltinn2 set
            _correspondenceStatusRepositoryMock.Verify(x => x.AddCorrespondenceStatus(
                It.Is<CorrespondenceStatusEntity>(e =>
                    e.CorrespondenceId == correspondenceId &&
                    e.Status == CorrespondenceStatus.PurgedByRecipient &&
                    e.StatusChanged.Equals(new DateTimeOffset(new DateTime(2025, 8, 1, 12, 0, 0))) &&
                    e.PartyUuid == partyUuid &&
                    e.SyncedFromAltinn2 != null),
                It.IsAny<CancellationToken>()),
                Times.Once);
            _correspondenceStatusRepositoryMock.VerifyNoOtherCalls();

            _correspondenceDeleteEventRepositoryMock.Verify(x => x.AddDeleteEvent(
               It.Is<CorrespondenceDeleteEventEntity>(e =>
                   e.EventType == CorrespondenceDeleteEventType.HardDeletedByRecipient
                   && e.PartyUuid == partyUuid
                   && e.EventOccurred.Equals(new DateTimeOffset(new DateTime(2025, 8, 1, 12, 0, 0)))
                   && e.SyncedFromAltinn2 != null),
               It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceDeleteEventRepositoryMock.Verify(x => x.GetDeleteEventsForCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceDeleteEventRepositoryMock.VerifyNoOtherCalls();

            // Should not trigger any additional Dialogporten changes or background jobs
            _backgroundJobClientMock.VerifyNoOtherCalls();
            _dialogPortenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task NotAvailable_PurgedByRecipient_AlreadyPurged()
        {
            // Arrange
            var partyUuid = Guid.NewGuid();

            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)
                .WithStatus(CorrespondenceStatus.PurgedByRecipient, new DateTime(2025, 8, 1, 12, 0, 0), partyUuid)
                .WithAltinn2CorrespondenceId(12345)
                .WithIsMigrating(true) // Not Available in Altinn 3                
                .Build();
            var correspondenceId = correspondence.Id;
            var recipient = correspondence.Recipient;
            var sender = correspondence.Sender;
            var request = new SyncCorrespondenceStatusEventRequest
            {
                CorrespondenceId = correspondenceId,
                SyncedEvents = new List<CorrespondenceStatusEntity>
                {
                    new CorrespondenceStatusEntity
                    {
                        Status = CorrespondenceStatus.PurgedByRecipient,
                        StatusChanged = new DateTimeOffset(new DateTime(2025, 8, 1, 12, 0, 0)),
                        PartyUuid = partyUuid
                    }
                }
            };

            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), true))
                .ReturnsAsync(correspondence);
            _correspondenceDeleteEventRepositoryMock
                .Setup(x => x.GetDeleteEventsForCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CorrespondenceDeleteEventEntity>());

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), true), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();

            // Verify that no statuses were added to Repository
            _correspondenceStatusRepositoryMock.VerifyNoOtherCalls();

            // Should not trigger any additional Dialogporten changes or background jobs
            _backgroundJobClientMock.VerifyNoOtherCalls();
            _dialogPortenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task NotAvailable_SoftDeleteByRecipient_OK()
        {
            // Arrange
            var partyUuid = Guid.NewGuid();

            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)
                .WithAltinn2CorrespondenceId(12345)
                .WithIsMigrating(true)
                .Build();
            var correspondenceId = correspondence.Id;
            var recipient = correspondence.Recipient;
            var sender = correspondence.Sender;
            var request = new SyncCorrespondenceStatusEventRequest
            {
                CorrespondenceId = correspondenceId,
                SyncedEvents = null,
                SyncedDeleteEvents = new List<CorrespondenceDeleteEventEntity>
                {
                    new CorrespondenceDeleteEventEntity
                    {
                        EventType = CorrespondenceDeleteEventType.SoftDeletedByRecipient,
                        EventOccurred = new DateTimeOffset(new DateTime(2025, 8, 1, 12, 0, 0)),
                        PartyUuid = partyUuid
                    }
                }
            };

            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), true))
                .ReturnsAsync(correspondence);
            _correspondenceStatusRepositoryMock
                .Setup(x => x.AddCorrespondenceStatuses(It.IsAny<List<CorrespondenceStatusEntity>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((List<CorrespondenceStatusEntity> r, CancellationToken _) => r);
            _correspondenceDeleteEventRepositoryMock
                .Setup(x => x.GetDeleteEventsForCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CorrespondenceDeleteEventEntity>());
            _correspondenceDeleteEventRepositoryMock
               .Setup(x => x.AddDeleteEvent(It.IsAny<CorrespondenceDeleteEventEntity>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((CorrespondenceDeleteEventEntity r, CancellationToken _) => r);
            _attachmentRepositoryMock
                .Setup(x => x.GetAttachmentsByCorrespondence(correspondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<AttachmentEntity>());

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), true), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();

            // Verify that the no status was added to Repository with SyncedFromAltinn2 set
            _correspondenceStatusRepositoryMock.VerifyNoOtherCalls();

            _correspondenceDeleteEventRepositoryMock.Verify(x => x.AddDeleteEvent(
                It.Is<CorrespondenceDeleteEventEntity>(e =>
                    e.EventType == CorrespondenceDeleteEventType.SoftDeletedByRecipient
                    && e.PartyUuid == partyUuid
                    && e.EventOccurred.Equals(new DateTimeOffset(new DateTime(2025, 8, 1, 12, 0, 0)))
                    && e.SyncedFromAltinn2 != null),
                It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceDeleteEventRepositoryMock.Verify(x => x.GetDeleteEventsForCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceDeleteEventRepositoryMock.VerifyNoOtherCalls();

            // Should not trigger any additional Dialogporten changes or background jobs
            _backgroundJobClientMock.VerifyNoOtherCalls();
            _dialogPortenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task NotAvailable_SoftDeleteByRecipientHardDeleteByRecipient_OK()
        {
            // Arrange
            var partyUuid = Guid.NewGuid();

            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)
                .WithAltinn2CorrespondenceId(12345)
                .WithIsMigrating(true)
                .Build();
            var correspondenceId = correspondence.Id;
            var recipient = correspondence.Recipient;
            var sender = correspondence.Sender;
            var request = new SyncCorrespondenceStatusEventRequest
            {
                CorrespondenceId = correspondenceId,
                SyncedEvents = null,
                SyncedDeleteEvents = new List<CorrespondenceDeleteEventEntity>
                {
                    new CorrespondenceDeleteEventEntity
                    {
                        EventType = CorrespondenceDeleteEventType.HardDeletedByRecipient,
                        EventOccurred = new DateTimeOffset(new DateTime(2025, 8, 15, 12, 0, 0)),
                        PartyUuid = partyUuid
                    },
                    new CorrespondenceDeleteEventEntity
                    {
                        EventType = CorrespondenceDeleteEventType.SoftDeletedByRecipient,
                        EventOccurred = new DateTimeOffset(new DateTime(2025, 8, 1, 12, 0, 0)),
                        PartyUuid = partyUuid
                    }
                }
            };

            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), true))
                .ReturnsAsync(correspondence);
            _correspondenceStatusRepositoryMock
                .Setup(x => x.AddCorrespondenceStatuses(It.IsAny<List<CorrespondenceStatusEntity>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((List<CorrespondenceStatusEntity> r, CancellationToken _) => r);
            _correspondenceDeleteEventRepositoryMock
                .Setup(x => x.GetDeleteEventsForCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CorrespondenceDeleteEventEntity>());
            _correspondenceDeleteEventRepositoryMock
               .Setup(x => x.AddDeleteEvent(It.IsAny<CorrespondenceDeleteEventEntity>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((CorrespondenceDeleteEventEntity r, CancellationToken _) => r);
            _attachmentRepositoryMock
                .Setup(x => x.GetAttachmentsByCorrespondence(correspondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<AttachmentEntity>());

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), true), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();

            // Verify that the no status was added to Repository with SyncedFromAltinn2 set
            _correspondenceStatusRepositoryMock.Verify(x => x.AddCorrespondenceStatus(
                It.Is<CorrespondenceStatusEntity>(e =>
                    e.CorrespondenceId == correspondenceId &&
                    e.Status == CorrespondenceStatus.PurgedByRecipient &&
                    e.StatusChanged.Equals(new DateTimeOffset(new DateTime(2025, 8, 15, 12, 0, 0))) &&
                    e.PartyUuid == partyUuid &&
                    e.SyncedFromAltinn2 != null),
                It.IsAny<CancellationToken>()),
                Times.Once);
            _correspondenceStatusRepositoryMock.VerifyNoOtherCalls();

            _correspondenceDeleteEventRepositoryMock.Verify(x => x.AddDeleteEvent(
                It.Is<CorrespondenceDeleteEventEntity>(e =>
                    e.EventType == CorrespondenceDeleteEventType.SoftDeletedByRecipient
                    && e.PartyUuid == partyUuid
                    && e.EventOccurred.Equals(new DateTimeOffset(new DateTime(2025, 8, 1, 12, 0, 0)))
                    && e.SyncedFromAltinn2 != null),
                It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceDeleteEventRepositoryMock.Verify(x => x.AddDeleteEvent(
                It.Is<CorrespondenceDeleteEventEntity>(e =>
                    e.EventType == CorrespondenceDeleteEventType.HardDeletedByRecipient
                    && e.PartyUuid == partyUuid
                    && e.EventOccurred.Equals(new DateTimeOffset(new DateTime(2025, 8, 15, 12, 0, 0)))
                    && e.SyncedFromAltinn2 != null),
                It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceDeleteEventRepositoryMock.Verify(x => x.GetDeleteEventsForCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceDeleteEventRepositoryMock.VerifyNoOtherCalls();

            // Should not trigger any additional Dialogporten changes or background jobs
            _backgroundJobClientMock.VerifyNoOtherCalls();
            _dialogPortenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task NotAvailable_RestoreByRecipient_OK()
        {
            // Arrange
            var partyUuid = Guid.NewGuid();

            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)
                .WithAltinn2CorrespondenceId(12345)
                .WithIsMigrating(true)
                .Build();
            var correspondenceId = correspondence.Id;
            var recipient = correspondence.Recipient;
            var sender = correspondence.Sender;
            var request = new SyncCorrespondenceStatusEventRequest
            {
                CorrespondenceId = correspondenceId,
                SyncedEvents = null,
                SyncedDeleteEvents = new List<CorrespondenceDeleteEventEntity>
                {
                    new CorrespondenceDeleteEventEntity
                    {
                        EventType = CorrespondenceDeleteEventType.RestoredByRecipient,
                        EventOccurred = new DateTimeOffset(new DateTime(2025, 8, 1, 12, 0, 0)),
                        PartyUuid = partyUuid
                    }
                }
            };

            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), true))
                .ReturnsAsync(correspondence);
            _correspondenceStatusRepositoryMock
                .Setup(x => x.AddCorrespondenceStatuses(It.IsAny<List<CorrespondenceStatusEntity>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((List<CorrespondenceStatusEntity> r, CancellationToken _) => r);
            _correspondenceDeleteEventRepositoryMock
                .Setup(x => x.GetDeleteEventsForCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CorrespondenceDeleteEventEntity>());
            _correspondenceDeleteEventRepositoryMock
               .Setup(x => x.AddDeleteEvent(It.IsAny<CorrespondenceDeleteEventEntity>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((CorrespondenceDeleteEventEntity r, CancellationToken _) => r);
            _attachmentRepositoryMock
                .Setup(x => x.GetAttachmentsByCorrespondence(correspondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<AttachmentEntity>());

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), true), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();

            // Verify that the no status was added to Repository with SyncedFromAltinn2 set
            _correspondenceStatusRepositoryMock.VerifyNoOtherCalls();

            _correspondenceDeleteEventRepositoryMock.Verify(x => x.AddDeleteEvent(
                It.Is<CorrespondenceDeleteEventEntity>(e =>
                    e.EventType == CorrespondenceDeleteEventType.RestoredByRecipient
                    && e.PartyUuid == partyUuid
                    && e.EventOccurred.Equals(new DateTimeOffset(new DateTime(2025, 8, 1, 12, 0, 0)))
                    && e.SyncedFromAltinn2 != null),
                It.IsAny<CancellationToken>()), Times.Once);            
            _correspondenceDeleteEventRepositoryMock.Verify(x => x.GetDeleteEventsForCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceDeleteEventRepositoryMock.VerifyNoOtherCalls();

            // Should not trigger any additional Dialogporten changes or background jobs
            _backgroundJobClientMock.VerifyNoOtherCalls();
            _dialogPortenServiceMock.VerifyNoOtherCalls();
        }

        private void VerifyAltinnEventEnqueued(Guid correspondenceId, AltinnEventType eventType, string recipient)
        {
            _backgroundJobClientMock.Verify(x => x.Create(
                It.Is<Job>(job => job.Method.Name == nameof(IEventBus.Publish) && (AltinnEventType)job.Args[0] == eventType && (string)job.Args[4] == recipient),
                It.IsAny<EnqueuedState>()));
        }

        private void VerifyDialogportenServiceCreateInformationActivityEnqueued(Guid correspondenceId, DialogportenActorType actorType, DialogportenTextType dpTextType, string recipient)
        {
            _backgroundJobClientMock.Verify(x => x.Create(
                It.Is<Job>(job => job.Method.Name == nameof(IDialogportenService.CreateConfirmedActivity) && (Guid)job.Args[0] == correspondenceId && (DialogportenActorType)job.Args[1] == actorType),
                It.IsAny<EnqueuedState>()));
        }

        private void VerifyDialogportenServiceCreateConfirmedActivityEnqueued(Guid correspondenceId, DialogportenActorType actorType, string recipient)
        {
            _backgroundJobClientMock.Verify(x => x.Create(
                It.Is<Job>(job => job.Method.Name == nameof(IDialogportenService.CreateConfirmedActivity) && (Guid)job.Args[0] == correspondenceId && (DialogportenActorType)job.Args[1] == actorType),
                It.IsAny<EnqueuedState>()));
        }

        private void VerifyDialogportenServiceCreatePurgedActivityEnqueued(Guid correspondenceId, DialogportenActorType actorType, string actorname, DateTimeOffset operationTimestamp)
        {
            _backgroundJobClientMock.Verify(x => x.Create(
                It.Is<Job>(job => job.Method.Name == nameof(IDialogportenService.CreateCorrespondencePurgedActivity) && (Guid)job.Args[0] == correspondenceId && (DialogportenActorType)job.Args[1] == actorType && (string)job.Args[2] == actorname && (DateTimeOffset)job.Args[3] == operationTimestamp),
                It.IsAny<EnqueuedState>()));
        }

        private void VerifyDialogportenServicePatchCorrespondenceDialogToConfirmedEnqueued(Guid correspondenceId)
        {
            _backgroundJobClientMock.Verify(x => x.Create(
                It.Is<Job>(job => job.Method.Name == nameof(IDialogportenService.PatchCorrespondenceDialogToConfirmed) && (Guid)job.Args[0] == correspondenceId),
                It.IsAny<EnqueuedState>()));
        }

        private void VerifyDialogportenServiceSetArchivedSystemLabelOnDialogEnqueued(Guid correspondenceId, string partyIdentifier)
        {
            _backgroundJobClientMock.Verify(x => x.Create(
                    It.Is<Job>(job => job.Method.Name == nameof(IDialogportenService.UpdateSystemLabelsOnDialog)
                        && (Guid)job.Args[0] == correspondenceId
                        && (string)job.Args[1] == partyIdentifier
                        && job.Args[2] != null
                        && ((List<string>)job.Args[2]).Contains("Archive")
                        && job.Args[3] == null),
                    It.IsAny<EnqueuedState>()));
        }

        private void VerifySoftDeleteUpdateForDialogportenEnqueued(Guid correspondenceId, string partyIdentifier, CorrespondenceDeleteEventType eventType)
        {
            if (eventType == CorrespondenceDeleteEventType.SoftDeletedByRecipient)
            {
                _backgroundJobClientMock.Verify(x => x.Create(
                    It.Is<Job>(job => job.Method.Name == nameof(IDialogportenService.UpdateSystemLabelsOnDialog)
                        && (Guid)job.Args[0] == correspondenceId
                        && (string)job.Args[1] == partyIdentifier
                        && job.Args[2] != null
                        && ((List<string>)job.Args[2]).Contains("Bin")
                        && job.Args[3] == null),
                    It.IsAny<EnqueuedState>()));
            }
            else if (eventType == CorrespondenceDeleteEventType.RestoredByRecipient)
            {
                _backgroundJobClientMock.Verify(x => x.Create(
                    It.Is<Job>(job => job.Method.Name == nameof(IDialogportenService.UpdateSystemLabelsOnDialog)
                        && (Guid)job.Args[0] == correspondenceId
                        && (string)job.Args[1] == partyIdentifier
                        && job.Args[2] == null
                        && job.Args[3] != null
                        && ((List<string>)job.Args[3]).Contains("Bin")),
                    It.IsAny<EnqueuedState>()));
            }
        }

        private void VerifyDialogportenServiceCreateOpenedActivityEnqueued(Guid correspondenceId)
        {
            _backgroundJobClientMock.Verify(x => x.Create(
                It.Is<Job>(job => job.Method.Name == nameof(IDialogportenService.CreateOpenedActivity) && (Guid)job.Args[0] == correspondenceId),
                It.IsAny<EnqueuedState>()));
        }

        private void VerifyDialogportenServiceSoftDeleteDialogEnqueued(string dialogId)
        {
            _backgroundJobClientMock.Verify(x => x.Create(
                It.Is<Job>(job => job.Method.Name == nameof(IDialogportenService.SoftDeleteDialog) && (string)job.Args[0] == dialogId),
                It.IsAny<EnqueuedState>()), Times.Once);
        }
    }
} 