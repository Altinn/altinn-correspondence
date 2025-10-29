using Altinn.Correspondence.Application.ExpireAttachment;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Hangfire;
using Hangfire.States;
using Microsoft.Extensions.Logging;
using Moq;

namespace Altinn.Correspondence.Tests.TestingHandler
{
    public class ExpireAttachmentHandlerTests
    {
        private readonly Mock<ILogger<ExpireAttachmentHandler>> _loggerMock = new();
        private readonly Mock<IAttachmentRepository> _attachmentRepositoryMock = new();
        private readonly Mock<IAttachmentStatusRepository> _attachmentStatusRepositoryMock = new();
        private readonly Mock<IStorageRepository> _storageRepositoryMock = new();
        private readonly Mock<IAltinnRegisterService> _altinnRegisterServiceMock = new();
        private readonly Mock<IBackgroundJobClient> _backgroundJobClientMock = new();
        private readonly ExpireAttachmentHandler _handler;

        public ExpireAttachmentHandlerTests()
        {
            _handler = new ExpireAttachmentHandler(
                _loggerMock.Object,
                _attachmentRepositoryMock.Object,
                _attachmentStatusRepositoryMock.Object,
                _storageRepositoryMock.Object,
                _altinnRegisterServiceMock.Object,
                _backgroundJobClientMock.Object);
        }

        [Fact]
        public async Task Process_AttachmentAlreadyPurged_IsIdempotent()
        {
            // Arrange
            var attachmentId = Guid.NewGuid();
            var attachment = new AttachmentEntity
            {
                Id = attachmentId,
                Sender = "urn:altinn:organization:identifier-no:991825827",
                SendersReference = "senders-reference-1",
                ResourceId = "res-1",
                Created = DateTimeOffset.UtcNow,
                AttachmentSize = 1,
                Statuses = new List<AttachmentStatusEntity>
                {
                    new AttachmentStatusEntity { AttachmentId = attachmentId, Status = AttachmentStatus.Purged, StatusChanged = DateTimeOffset.UtcNow.AddMinutes(-1) }
                }
            };
            _attachmentRepositoryMock
                .Setup(r => r.GetAttachmentById(attachmentId, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(attachment);

            // Act
            var result = await _handler.Process(attachmentId, null, CancellationToken.None);

            // Assert
            Assert.True(result.IsT0);
            _attachmentStatusRepositoryMock.Verify(x => x.AddAttachmentStatus(It.IsAny<AttachmentStatusEntity>(), It.IsAny<CancellationToken>()), Times.Never);
            _storageRepositoryMock.Verify(x => x.PurgeAttachment(It.IsAny<Guid>(), It.IsAny<StorageProviderEntity?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Process_Succeeds_AddsStatusPurged_PurgesBlob_PublishesEvent()
        {
            // Arrange
            var attachmentId = Guid.NewGuid();
            var partyUuid = Guid.NewGuid();
            var attachment = new AttachmentEntity
            {
                Id = attachmentId,
                Sender = "urn:altinn:organization:identifier-no:991825827",
                SendersReference = "senders-reference-1",
                ResourceId = "res-1",
                Created = DateTimeOffset.UtcNow,
                AttachmentSize = 1,
                StorageProvider = new StorageProviderEntity { StorageResourceName = "storage-resource-name-1", ServiceOwnerId = "service-owner-id-1", Active = true },
                Statuses = new List<AttachmentStatusEntity>(),
                ExpirationTime = DateTimeOffset.UtcNow.AddDays(-1)
            };
            _attachmentRepositoryMock
                .Setup(r => r.GetAttachmentById(attachmentId, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(attachment);
            _altinnRegisterServiceMock
                .Setup(s => s.LookUpPartyById(attachment.Sender, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Party { PartyUuid = partyUuid });

            // Act
            var result = await _handler.Process(attachmentId, null, CancellationToken.None);

            // Assert
            Assert.True(result.IsT0);
            _attachmentStatusRepositoryMock.Verify(x => x.AddAttachmentStatus(
                It.Is<AttachmentStatusEntity>(s => s.AttachmentId == attachmentId && s.Status == AttachmentStatus.Purged && s.StatusText == "The attachment has expired"),
                It.IsAny<CancellationToken>()), Times.Once);

            _storageRepositoryMock.Verify(x => x.PurgeAttachment(attachmentId, attachment.StorageProvider, It.IsAny<CancellationToken>()), Times.Once);

            _backgroundJobClientMock.Verify(x => x.Create(
                It.Is<Hangfire.Common.Job>(job => job.Type == typeof(IEventBus) && job.Method.Name == "Publish"),
                It.IsAny<IState>()), Times.Once);
        }
    }
}
