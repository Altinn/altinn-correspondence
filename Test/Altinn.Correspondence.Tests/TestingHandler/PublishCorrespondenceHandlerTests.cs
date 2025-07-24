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
        }

        private void SetupCommonMocks(Guid correspondenceId, Guid partyUuid, CorrespondenceEntity correspondence)
        {
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

        private void SetupBrregServiceWithRoles(string organizationNumber, OrganizationRoles organizationRoles)
        {
            _brregServiceMock
                .Setup(x => x.GetOrganizationRoles(organizationNumber, It.IsAny<CancellationToken>()))
                .Returns((string id, CancellationToken token) =>
                    Task.FromResult(organizationRoles));
        }

        private void SetupBrregServiceWithOrgDetails(string organizationNumber, bool isBankrupt = false)
        {
            _brregServiceMock
                .Setup(x => x.GetOrganizationDetails(organizationNumber, It.IsAny<CancellationToken>()))
                .Returns((string id, CancellationToken token) =>
                    Task.FromResult(new OrganizationDetails { IsBankrupt = isBankrupt }));
        }

        private void SetupBrregServiceWithSubOrgDetails(string organizationNumber, string parentOrganizationNumber, bool isBankrupt = false)
        {
            _brregServiceMock
                .Setup(x => x.GetSubOrganizationDetails(organizationNumber, It.IsAny<CancellationToken>()))
                .Returns((string id, CancellationToken token) =>
                    Task.FromResult(new SubOrganizationDetails { IsBankrupt = isBankrupt, ParentOrganizationNumber = parentOrganizationNumber }));
        }

        private void SetupBrregServiceToThrowNotFoundForOrg(string organizationNumber)
        {
            _brregServiceMock
                .Setup(x => x.GetOrganizationDetails(organizationNumber, It.IsAny<CancellationToken>()))
                .Throws(new BrregNotFoundException(organizationNumber));
        }

        private OrganizationRoles CreateOrganizationRolesWithRole(string roleCode)
        {
            return new OrganizationRoles
            {
                RoleGroups = new List<RoleGroup>
                {
                    new RoleGroup
                    {
                        Roles = new List<Role>
                        {
                            new Role
                            {
                                Type = new TypeInfo { Code = roleCode },
                                HasResigned = false,
                                Person = new Person { IsDead = false }
                            }
                        }
                    }
                }
            };
        }

        private OrganizationRoles CreateOrganizationRolesWithoutRequiredRoles()
        {
            return CreateOrganizationRolesWithRole("ANNET");
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
            SetupBrregServiceWithOrgDetails("310244007");
            SetupBrregServiceWithRoles("310244007", CreateOrganizationRolesWithoutRequiredRoles());

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
            SetupBrregServiceWithOrgDetails("310244007");
            SetupBrregServiceWithRoles("310244007", CreateOrganizationRolesWithRole("BEST"));

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

        [Fact]
        public async Task Process_WhenOrganizationNotFoundInBrreg_FailsCorrespondenceWithCorrectMessage()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            var partyUuid = Guid.NewGuid();
            var senderUrn = "urn:altinn:organization:identifier-no:313721779";
            var recipientUrn = "urn:altinn:organization:identifier-no:310244007";
            var organizationNumber = "310244007";
            
            var correspondence = CreateTestCorrespondence(correspondenceId, senderUrn, recipientUrn);
            SetupCommonMocks(correspondenceId, partyUuid, correspondence);
            SetupBrregServiceToThrowNotFoundForOrg(organizationNumber);

            _brregServiceMock
                .Setup(x => x.GetSubOrganizationDetails(organizationNumber, It.IsAny<CancellationToken>()))
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

            _slackClientMock.Verify(
                x => x.PostAsync(It.Is<SlackMessage>(m => 
                    m.Text.Contains("Correspondence failed"))),
                Times.Once);
        }

        [Fact]
        public async Task Process_CorrespondenceWithSubOrgRecipientThatHasParentOrgWithRequiredRoles_Succeeds()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            var partyUuid = Guid.NewGuid();
            var senderUrn = "urn:altinn:organization:identifier-no:313721779";
            var recipientUrn = "urn:altinn:organization:identifier-no:310244007";
            
            var correspondence = CreateTestCorrespondence(correspondenceId, senderUrn, recipientUrn);
            SetupCommonMocks(correspondenceId, partyUuid, correspondence);
            SetupBrregServiceToThrowNotFoundForOrg("310244007");
            SetupBrregServiceWithSubOrgDetails("310244007", "313721779");
            SetupBrregServiceWithRoles("313721779", CreateOrganizationRolesWithRole("BEST"));

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

        [Fact]
        public async Task Process_CorrespondenceWithSubOrgRecipientThatHasParentOrgWithoutRequiredRoles_Fails()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            var partyUuid = Guid.NewGuid();
            var senderUrn = "urn:altinn:organization:identifier-no:313721779";
            var recipientUrn = "urn:altinn:organization:identifier-no:310244007";
            
            var correspondence = CreateTestCorrespondence(correspondenceId, senderUrn, recipientUrn);
            SetupCommonMocks(correspondenceId, partyUuid, correspondence);
            SetupBrregServiceToThrowNotFoundForOrg("310244007");
            SetupBrregServiceWithSubOrgDetails("310244007", "313721779");
            SetupBrregServiceWithRoles("313721779", CreateOrganizationRolesWithoutRequiredRoles());

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
    }
} 