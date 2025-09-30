using Altinn.Correspondence.Application.PublishCorrespondence;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Models.Register;
using Altinn.Correspondence.Integrations.Redlock;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Slack.Webhooks;

namespace Altinn.Correspondence.Tests.TestingHandler
{
    public class PublishCorrespondenceHandlerTests
    {
        private readonly Mock<IAltinnRegisterService> _altinnRegisterServiceMock;
        private readonly Mock<ILogger<PublishCorrespondenceHandler>> _loggerMock;
        private readonly Mock<ICorrespondenceRepository> _correspondenceRepositoryMock;
        private readonly Mock<ICorrespondenceStatusRepository> _correspondenceStatusRepositoryMock;
        private readonly Mock<IContactReservationRegistryService> _contactReservationRegistryServiceMock;
        private readonly Mock<IHostEnvironment> _hostEnvironmentMock;
        private readonly Mock<ISlackClient> _slackClientMock;
        private readonly Mock<IBackgroundJobClient> _backgroundJobClientMock;
        private readonly Mock<IDistributedLockHelper> _distributedLockHelperMock;
        private readonly SlackSettings _slackSettings;
        private readonly PublishCorrespondenceHandler _handler;

        public PublishCorrespondenceHandlerTests()
        {
            _altinnRegisterServiceMock = new Mock<IAltinnRegisterService>();
            _loggerMock = new Mock<ILogger<PublishCorrespondenceHandler>>();
            _correspondenceRepositoryMock = new Mock<ICorrespondenceRepository>();
            _correspondenceStatusRepositoryMock = new Mock<ICorrespondenceStatusRepository>();
            _contactReservationRegistryServiceMock = new Mock<IContactReservationRegistryService>();
            _hostEnvironmentMock = new Mock<IHostEnvironment>();
            _slackClientMock = new Mock<ISlackClient>();
            _backgroundJobClientMock = new Mock<IBackgroundJobClient>();
            _distributedLockHelperMock = new Mock<IDistributedLockHelper>();
            _slackSettings = new SlackSettings(_hostEnvironmentMock.Object);
            _backgroundJobClientMock
                .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
                .Returns(() => Guid.NewGuid().ToString());

            _handler = new PublishCorrespondenceHandler(
                _altinnRegisterServiceMock.Object,
                _loggerMock.Object,
                _correspondenceRepositoryMock.Object,
                _correspondenceStatusRepositoryMock.Object,
                _contactReservationRegistryServiceMock.Object,
                _hostEnvironmentMock.Object,
                _slackClientMock.Object,
                _slackSettings,
                _backgroundJobClientMock.Object,
                _distributedLockHelperMock.Object);
        }

        private void SetupCommonMocks(Guid correspondenceId, Guid partyUuid, CorrespondenceEntity correspondence)
        {
            // Mock party lookup
            _altinnRegisterServiceMock
                .Setup(x => x.LookUpPartyById(
                    It.IsAny<string>(), 
                    It.IsAny<CancellationToken>()))
                .Returns((string id, CancellationToken token) => 
                {
                    var party = new Party { PartyUuid = partyUuid };
                    // Ensure recipient is treated as organization (non-empty OrgNumber) to trigger role checks
                    if (id.Contains("310244007") || id == correspondence.Recipient)
                    {
                        party.OrgNumber = "310244007";
                    }
                    return Task.FromResult<Party?>(party);
                });

            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceById(correspondenceId, true, true, false, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(correspondence);

            _correspondenceRepositoryMock
                .Setup(x => x.AreAllAttachmentsPublished(correspondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            
            // Default setup for DistributedLockHelper to return acquired lock and not skip
            _distributedLockHelperMock
                .Setup(x => x.ExecuteWithConditionalLockAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<CancellationToken, Task<bool>>>(),
                    It.IsAny<Func<CancellationToken, Task>>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((false, true));
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

        private List<RoleItem> CreateRoleItems(params string[] identifiers) => identifiers
            .Select(code => new RoleItem { Role = new RoleDescriptor { Identifier = code } })
            .ToList();

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
            _altinnRegisterServiceMock
                .Setup(x => x.LookUpPartyRoles(partyUuid.ToString(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateRoleItems("ANNET"));

            // Act
            await _handler.ProcessWithLock(correspondenceId, null, CancellationToken.None);

            // Assert
            _correspondenceStatusRepositoryMock.Verify(
                x => x.AddCorrespondenceStatus(
                    It.Is<CorrespondenceStatusEntity>(s => 
                        s.CorrespondenceId == correspondenceId && 
                        s.Status == CorrespondenceStatus.Failed && 
                        s.StatusText.Contains("lacks roles")),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _slackClientMock.Verify(
                x => x.PostAsync(It.Is<SlackMessage>(m => m.Text.Contains("Correspondence failed"))),
                Times.Once);
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
            _altinnRegisterServiceMock
                .Setup(x => x.LookUpPartyRoles(partyUuid.ToString(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateRoleItems("daglig-leder"));

            // Act
            await _handler.ProcessWithLock(correspondenceId, null, CancellationToken.None);

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

            _slackClientMock.Verify(
                x => x.PostAsync(It.Is<SlackMessage>(m => m.Text.Contains("Correspondence failed"))),
                Times.Never);
        }
    }
} 