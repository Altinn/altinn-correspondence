using Altinn.Correspondence.Application.PublishCorrespondence;
using Altinn.Correspondence.Core.Models.Brreg;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Redlock;
using Hangfire;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Slack.Webhooks;
using Altinn.Correspondence.Core.Exceptions;

namespace Altinn.Correspondence.Tests.TestingHandler
{
    public class PublishCorrespondenceHandlerTests
    {
        private readonly Mock<IAltinnRegisterService> _altinnRegisterServiceMock;
        private readonly Mock<ILogger<PublishCorrespondenceHandler>> _loggerMock;
        private readonly Mock<ICorrespondenceRepository> _correspondenceRepositoryMock;
        private readonly Mock<ICorrespondenceStatusRepository> _correspondenceStatusRepositoryMock;
        private readonly Mock<IContactReservationRegistryService> _contactReservationRegistryServiceMock;
        private readonly Mock<IDialogportenService> _dialogportenServiceMock;
        private readonly Mock<IHostEnvironment> _hostEnvironmentMock;
        private readonly Mock<ISlackClient> _slackClientMock;
        private readonly Mock<IBackgroundJobClient> _backgroundJobClientMock;
        private readonly Mock<IBrregService> _brregServiceMock;
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
            _dialogportenServiceMock = new Mock<IDialogportenService>();
            _hostEnvironmentMock = new Mock<IHostEnvironment>();
            _slackClientMock = new Mock<ISlackClient>();
            _backgroundJobClientMock = new Mock<IBackgroundJobClient>();
            _brregServiceMock = new Mock<IBrregService>();
            _distributedLockHelperMock = new Mock<IDistributedLockHelper>();
            _slackSettings = new SlackSettings(_hostEnvironmentMock.Object);

            _handler = new PublishCorrespondenceHandler(
                _altinnRegisterServiceMock.Object,
                _loggerMock.Object,
                _correspondenceRepositoryMock.Object,
                _correspondenceStatusRepositoryMock.Object,
                _contactReservationRegistryServiceMock.Object,
                _dialogportenServiceMock.Object,
                _hostEnvironmentMock.Object,
                _slackClientMock.Object,
                _slackSettings,
                _backgroundJobClientMock.Object,
                _brregServiceMock.Object,
                _distributedLockHelperMock.Object);

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

        [Fact]
        public async Task Process_ConfidentialCorrespondenceWithOrgRecipientMissingRequiredRoles_FailsCorrespondence()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            var partyUuid = Guid.NewGuid();
            var senderUrn = "urn:altinn:organization:identifier-no:313721779";
            var recipientUrn = "urn:altinn:organization:identifier-no:310244007";
            var now = DateTimeOffset.UtcNow;
            
            var correspondence = new CorrespondenceEntity
            {
                Id = correspondenceId,
                Sender = senderUrn,
                Recipient = recipientUrn,
                IsConfidential = true,
                ResourceId = "resource-123",
                SendersReference = "ref-123",
                RequestedPublishTime = now.AddMinutes(-10),
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

            // Mock party lookup
            _altinnRegisterServiceMock
                .Setup(x => x.LookUpPartyById(
                    It.IsAny<string>(), 
                    It.IsAny<CancellationToken>()))
                .Returns((string id, CancellationToken token) => 
                    Task.FromResult<Party?>(new Party { PartyUuid = partyUuid }));

            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceById(correspondenceId, true, true, false, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(correspondence);

            // Mock Brreg service - to return organization roles without required roles
            var organizationRoles = new OrganizationRoles
            {
                RoleGroups = new List<RoleGroup>
                {
                    new RoleGroup
                    {
                        Roles = new List<Role>
                        {
                            new Role
                            {
                                Type = new TypeInfo { Code = "ANNET" }, // Not in the required roles list
                                HasResigned = false
                            }
                        }
                    }
                }
            };

            _brregServiceMock
                .Setup(x => x.GetOrganizationDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((string id, CancellationToken token) =>
                    Task.FromResult(new OrganizationDetails { IsBankrupt = false }));

            _brregServiceMock
                .Setup(x => x.GetOrganizationRolesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((string id, CancellationToken token) =>
                    Task.FromResult(organizationRoles));

            // Act
            await _handler.ProcessWithLock(correspondenceId, null, CancellationToken.None);

            // Assert
            _correspondenceStatusRepositoryMock.Verify(
                x => x.AddCorrespondenceStatus(
                    It.Is<CorrespondenceStatusEntity>(s => 
                        s.CorrespondenceId == correspondenceId && 
                        s.Status == CorrespondenceStatus.Failed && 
                        s.StatusText.Contains("missing required roles")),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify slack notification was sent using PostAsync
            _slackClientMock.Verify(
                x => x.PostAsync(It.Is<SlackMessage>(m => m.Text.Contains("Correspondence failed"))),
                Times.Once);
        }

        [Fact]
        public async Task Process_ConfidentialCorrespondenceWithOrgRecipientHavingRequiredRoles_Succeeds()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            var partyUuid = Guid.NewGuid();
            var senderUrn = "urn:altinn:organization:identifier-no:313721779";
            var recipientUrn = "urn:altinn:organization:identifier-no:310244007";
            var now = DateTimeOffset.UtcNow;
            
            var correspondence = new CorrespondenceEntity
            {
                Id = correspondenceId,
                Sender = senderUrn,
                Recipient = recipientUrn,
                IsConfidential = true,
                ResourceId = "resource-123",
                SendersReference = "ref-123",
                RequestedPublishTime = now.AddMinutes(-10),
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
                        StatusChanged = now.AddMinutes(-10),
                        StatusText = CorrespondenceStatus.ReadyForPublish.ToString()
                    }
                }
            };

            // Mock party lookup
            _altinnRegisterServiceMock
                .Setup(x => x.LookUpPartyById(
                    It.IsAny<string>(), 
                    It.IsAny<CancellationToken>()))
                .Returns((string id, CancellationToken token) => 
                    Task.FromResult<Party?>(new Party { PartyUuid = partyUuid }));

            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceById(correspondenceId, true, true, false, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(correspondence);

            _correspondenceRepositoryMock
                .Setup(x => x.AreAllAttachmentsPublished(correspondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Mock Brreg service - return organization roles with required roles
            var organizationRoles = new OrganizationRoles
            {
                RoleGroups = new List<RoleGroup>
                {
                    new RoleGroup
                    {
                        Roles = new List<Role>
                        {
                            new Role
                            {
                                Type = new TypeInfo { Code = "BEST" }, // In required roles list
                                HasResigned = false
                            }
                        }
                    }
                }
            };

            _brregServiceMock
                .Setup(x => x.GetOrganizationDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((string id, CancellationToken token) =>
                    Task.FromResult(new OrganizationDetails { IsBankrupt = false }));

            _brregServiceMock
                .Setup(x => x.GetOrganizationRolesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((string id, CancellationToken token) =>
                    Task.FromResult(organizationRoles));

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

            // Verify slack notification was NOT sent (no error)
            _slackClientMock.Verify(
                x => x.PostAsync(It.Is<SlackMessage>(m => m.Text.Contains("Correspondence failed"))),
                Times.Never);
        }

        [Fact]
        public async Task Process_WhenOrganizationNotFoundInBrreg_FailsCorrespondenceWithCorrectMessage()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            var partyUuid = Guid.NewGuid();
            var senderUrn = "urn:altinn:organization:identifier-no:313721779";
            var recipientUrn = "urn:altinn:organization:identifier-no:310244007";
            var organizationNumber = "310244007"; // Extract just the org number for exception
            var now = DateTimeOffset.UtcNow;
            
            var correspondence = new CorrespondenceEntity
            {
                Id = correspondenceId,
                Sender = senderUrn,
                Recipient = recipientUrn,
                IsConfidential = true,
                ResourceId = "resource-123",
                SendersReference = "ref-123",
                RequestedPublishTime = now.AddMinutes(-10),
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
                        StatusChanged = now.AddMinutes(-10),
                        StatusText = CorrespondenceStatus.ReadyForPublish.ToString()
                    }
                }
            };

            // Mock party lookup
            _altinnRegisterServiceMock
                .Setup(x => x.LookUpPartyById(
                    It.IsAny<string>(), 
                    It.IsAny<CancellationToken>()))
                .Returns((string id, CancellationToken token) => 
                    Task.FromResult<Party?>(new Party { PartyUuid = partyUuid }));

            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceById(correspondenceId, true, true, false, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(correspondence);

            // Mock Brreg service to throw BrregNotFoundException
            _brregServiceMock
                .Setup(x => x.GetOrganizationDetailsAsync(organizationNumber, It.IsAny<CancellationToken>()))
                .Throws(new BrregNotFoundException(organizationNumber));

            // Act
            await _handler.ProcessWithLock(correspondenceId, null, CancellationToken.None);

            // Assert
            _correspondenceStatusRepositoryMock.Verify(
                x => x.AddCorrespondenceStatus(
                    It.Is<CorrespondenceStatusEntity>(s => 
                        s.CorrespondenceId == correspondenceId && 
                        s.Status == CorrespondenceStatus.Failed && 
                        s.StatusText.Contains("not found in 'Enhetsregisteret'")),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify slack notification was sent
            _slackClientMock.Verify(
                x => x.PostAsync(It.Is<SlackMessage>(m => 
                    m.Text.Contains("Correspondence failed") && 
                    m.Attachments != null && 
                    m.Attachments.Any(a => a.Text != null && a.Text.Contains("not found in 'Enhetsregisteret'")))),
                Times.Once);
        }
    }
} 