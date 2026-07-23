using Moq;
using System.Text.Json;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Tests.Extensions;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Application.CreateNotificationOrder;
using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Core.Models.Profile;
using Altinn.Notifications.Core.Helpers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Tests.TestingHandler
{
    public class CreateNotificationOrderHandlerTests
    {
        private readonly Mock<ICorrespondenceRepository> _mockCorrespondenceRepository;
        private readonly Mock<INotificationTemplateRepository> _mockNotificationTemplateRepository;
        private readonly Mock<IAltinnRegisterService> _mockAltinnRegisterService;
        private readonly Mock<IAltinnProfileService> _mockAltinnProfileService;
        private readonly Mock<ICorrespondenceNotificationRepository> _mockCorrespondenceNotificationRepository;
        private readonly Mock<IIdempotencyKeyRepository> _mockIdempotencyKeyRepository;
        private readonly Mock<IResourceRegistryService> _mockResourceRegistryService;
        private readonly Mock<IHostEnvironment> _mockHostEnvironment;
        private readonly Mock<IOptions<GeneralSettings>> _mockGeneralSettings;
        private readonly Mock<ILogger<CreateNotificationOrderHandler>> _mockLogger;
        private readonly CreateNotificationOrderHandler _handler;

        public CreateNotificationOrderHandlerTests()
        {
            _mockCorrespondenceRepository = new Mock<ICorrespondenceRepository>();
            _mockNotificationTemplateRepository = new Mock<INotificationTemplateRepository>();
            _mockAltinnRegisterService = new Mock<IAltinnRegisterService>();
            _mockAltinnProfileService = new Mock<IAltinnProfileService>();
            _mockCorrespondenceNotificationRepository = new Mock<ICorrespondenceNotificationRepository>();
            _mockIdempotencyKeyRepository = new Mock<IIdempotencyKeyRepository>();
            _mockResourceRegistryService = new Mock<IResourceRegistryService>();
            _mockHostEnvironment = new Mock<IHostEnvironment>();
            _mockGeneralSettings = new Mock<IOptions<GeneralSettings>>();
            _mockLogger = new Mock<ILogger<CreateNotificationOrderHandler>>();

            _mockGeneralSettings.Setup(x => x.Value).Returns(new GeneralSettings
            {
                CorrespondenceBaseUrl = "https://test.altinn.no"
            });
            _mockHostEnvironment.Setup(x => x.EnvironmentName).Returns("Development");
            _mockAltinnRegisterService.Setup(x => x.LookUpPartyById(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(RegisterServiceMockExtensions.BuildOrganization(Guid.NewGuid(), "991825827", displayName: "Name"));
            _mockAltinnProfileService
                .Setup(x => x.GetOrganizationNotificationAddresses(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OrgNotificationAddresses>());
            _mockAltinnProfileService
                .Setup(x => x.GetUserRegisteredContactPoints(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<UnitContactPoints>());
            _mockResourceRegistryService
                .Setup(x => x.GetResourceTitle(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("Resource Title");

            _handler = new CreateNotificationOrderHandler(
                _mockCorrespondenceRepository.Object,
                _mockAltinnRegisterService.Object,
                _mockAltinnProfileService.Object,
                _mockNotificationTemplateRepository.Object,
                _mockCorrespondenceNotificationRepository.Object,
                _mockIdempotencyKeyRepository.Object,
                _mockResourceRegistryService.Object,
                _mockHostEnvironment.Object,
                _mockGeneralSettings.Object,
                new MobileNumberHelper(),
                _mockLogger.Object,
                TestDbContextFactory.Create());
        }

        private (CreateNotificationOrderRequest request, CorrespondenceEntity correspondence, NotificationTemplateEntity template) SetupOrderData(DateTimeOffset requestedPublishTime)
        {
            var correspondenceId = Guid.NewGuid();
            var request = new CreateNotificationOrderRequest
            {
                CorrespondenceId = correspondenceId,
                NotificationRequest = new NotificationRequest
                {
                    NotificationTemplate = NotificationTemplate.GenericAltinnMessage,
                    NotificationChannel = NotificationChannel.EmailPreferred,
                    SendReminder = true,
                    EmailSubject = "Test Subject",
                    EmailBody = "Test Body",
                    ReminderEmailSubject = "Reminder Subject",
                    ReminderEmailBody = "Reminder Body",
                    ReminderNotificationChannel = NotificationChannel.SmsPreferred
                },
                Language = "nb"
            };

            var correspondence = new CorrespondenceEntity
            {
                Id = correspondenceId,
                ResourceId = "resource1",
                SendersReference = "ref1",
                Recipient = "urn:altinn:person:identifier-no:08900499559",
                RequestedPublishTime = requestedPublishTime,
                Sender = "sender",
                Statuses = new List<CorrespondenceStatusEntity>(),
                Content = new CorrespondenceContentEntity
                {
                    Language = "nb",
                    MessageTitle = "Default title",
                    MessageSummary = "Default summary",
                    MessageBody = "Default body",
                    Attachments = new List<CorrespondenceAttachmentEntity>()
                },
                Created = DateTimeOffset.UtcNow
            };

            var template = new NotificationTemplateEntity
            {
                Id = 1,
                Template = NotificationTemplate.GenericAltinnMessage,
                Language = "nb",
                EmailSubject = "Test Subject",
                EmailBody = "Test Body",
                SmsBody = "Test SMS",
                ReminderEmailSubject = "Reminder Subject",
                ReminderEmailBody = "Reminder Body",
                ReminderSmsBody = "Reminder SMS"
            };

            _mockCorrespondenceRepository
                .Setup(x => x.GetCorrespondenceById(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), CancellationToken.None, false))
                .ReturnsAsync(correspondence);

            _mockNotificationTemplateRepository
                .Setup(x => x.GetNotificationTemplates(It.IsAny<NotificationTemplate>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ReturnsAsync(new List<NotificationTemplateEntity> { template });

            return (request, correspondence, template);
        }

        [Fact]
        public async Task Process_ShouldPersistOrder_WithCorrectFields()
        {
            var requestedPublishTime = DateTimeOffset.UtcNow.AddMinutes(10);
            var (request, correspondence, _) = SetupOrderData(requestedPublishTime);

            await _handler.Process(request, CancellationToken.None);

            var expectedMainTime = requestedPublishTime.UtcDateTime;
            _mockCorrespondenceNotificationRepository.Verify(x => x.AddNotification(It.Is<CorrespondenceNotificationEntity>(n =>
                n.NotificationTemplate == NotificationTemplate.GenericAltinnMessage &&
                n.NotificationChannel == NotificationChannel.EmailPreferred &&
                n.CorrespondenceId == correspondence.Id &&
                !n.IsReminder &&
                n.RequestedSendTime == expectedMainTime &&
                n.OrderRequest != null &&
                JsonSerializer.Deserialize<NotificationOrderRequestV2>(n.OrderRequest!, (JsonSerializerOptions?)null) != null
            ), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task Process_ShouldDeduplicateRecipients_WithSameIdentifier()
        {
            // Arrange
            var requestedPublishTime = DateTimeOffset.UtcNow.AddMinutes(10);
            var (request, _, _) = SetupOrderData(requestedPublishTime);

            request.NotificationRequest.OverrideRegisteredContactInformation = true;
            var nin = "26818099001";
            request.NotificationRequest.CustomRecipients =
            [
                new Recipient { NationalIdentityNumber = nin },
                new Recipient { NationalIdentityNumber = nin }
            ];

            // Act
            await _handler.Process(request, CancellationToken.None);

            // Assert: idempotency key and notification are each created only once
            _mockIdempotencyKeyRepository.Verify(
                x => x.CreateAsync(It.IsAny<IdempotencyKeyEntity>(), It.IsAny<CancellationToken>()),
                Times.Once);

            _mockCorrespondenceNotificationRepository.Verify(
                x => x.AddNotification(It.IsAny<CorrespondenceNotificationEntity>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Process_ShouldUseNow_WhenPublishTimeInPast_Production()
        {
            _mockHostEnvironment.Setup(x => x.EnvironmentName).Returns("Production");
            var requestedPublishTime = DateTimeOffset.UtcNow.AddMinutes(-10);
            var (request, _, _) = SetupOrderData(requestedPublishTime);

            var before = DateTime.UtcNow.AddSeconds(-20);
            await _handler.Process(request, CancellationToken.None);
            var after = DateTime.UtcNow.AddSeconds(20);

            _mockCorrespondenceNotificationRepository.Verify(x => x.AddNotification(It.Is<CorrespondenceNotificationEntity>(n =>
                n.RequestedSendTime >= before && n.RequestedSendTime <= after
            ), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task Process_ShouldSetConditionEndpoint_InOrderRequest_WhenSendReminderIsTrue()
        {
            var requestedPublishTime = DateTimeOffset.UtcNow.AddMinutes(10);
            var (request, correspondence, _) = SetupOrderData(requestedPublishTime);

            CorrespondenceNotificationEntity? captured = null;
            _mockCorrespondenceNotificationRepository
                .Setup(x => x.AddNotification(It.IsAny<CorrespondenceNotificationEntity>(), It.IsAny<CancellationToken>()))
                .Callback<CorrespondenceNotificationEntity, CancellationToken>((n, _) => captured = n)
                .ReturnsAsync(Guid.NewGuid());

            await _handler.Process(request, CancellationToken.None);

            Assert.NotNull(captured);
            var deserialized = JsonSerializer.Deserialize<NotificationOrderRequestV2>(captured!.OrderRequest!);
            Assert.NotNull(deserialized);
            Assert.NotNull(deserialized!.Reminders);
            Assert.True(deserialized.Reminders!.Count >= 1);
            Assert.NotNull(deserialized.Reminders![0].ConditionEndpoint);
            Assert.Contains($"/correspondence/api/v1/correspondence/{correspondence.Id}/notification/check", deserialized.Reminders![0].ConditionEndpoint!);
        }

        [Fact]
        public async Task Process_ShouldReplaceResourceNameKeyword_InNotificationTexts()
        {
            var requestedPublishTime = DateTimeOffset.UtcNow.AddMinutes(10);
            var (request, _, template) = SetupOrderData(requestedPublishTime);

            template.EmailSubject = "Hello $resourceName$";

            CorrespondenceNotificationEntity? captured = null;
            _mockCorrespondenceNotificationRepository
                .Setup(x => x.AddNotification(It.IsAny<CorrespondenceNotificationEntity>(), It.IsAny<CancellationToken>()))
                .Callback<CorrespondenceNotificationEntity, CancellationToken>((n, _) => captured = n)
                .ReturnsAsync(Guid.NewGuid());

            await _handler.Process(request, CancellationToken.None);

            Assert.NotNull(captured);
            var deserialized = JsonSerializer.Deserialize<NotificationOrderRequestV2>(captured!.OrderRequest!);
            Assert.NotNull(deserialized);
            Assert.NotNull(deserialized!.Recipient.RecipientPerson);
            Assert.NotNull(deserialized!.Recipient.RecipientPerson!.EmailSettings);
            Assert.Equal("Hello Resource Title", deserialized.Recipient.RecipientPerson.EmailSettings!.Subject);
        }

        [Fact]
        public async Task Process_ShouldReplaceMessageTitleKeyword_InNotificationTexts()
        {
            var requestedPublishTime = DateTimeOffset.UtcNow.AddMinutes(10);
            var (request, correspondence, template) = SetupOrderData(requestedPublishTime);

            correspondence.Content!.MessageTitle = "My correspondence title";
            template.EmailSubject = "Title: $messageTitle$";

            CorrespondenceNotificationEntity? captured = null;
            _mockCorrespondenceNotificationRepository
                .Setup(x => x.AddNotification(It.IsAny<CorrespondenceNotificationEntity>(), It.IsAny<CancellationToken>()))
                .Callback<CorrespondenceNotificationEntity, CancellationToken>((n, _) => captured = n)
                .ReturnsAsync(Guid.NewGuid());

            await _handler.Process(request, CancellationToken.None);

            Assert.NotNull(captured);
            var deserialized = JsonSerializer.Deserialize<NotificationOrderRequestV2>(captured!.OrderRequest!);
            Assert.NotNull(deserialized);
            Assert.NotNull(deserialized!.Recipient.RecipientPerson);
            Assert.NotNull(deserialized!.Recipient.RecipientPerson!.EmailSettings);
            Assert.Equal("Title: My correspondence title", deserialized.Recipient.RecipientPerson.EmailSettings!.Subject);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [InlineData(null)]
        public async Task Process_ShouldPassIgnoreReservation_ToRecipientPerson(bool? ignoreReservation)
        {
            var requestedPublishTime = DateTimeOffset.UtcNow.AddMinutes(10);
            var (request, correspondence, _) = SetupOrderData(requestedPublishTime);
            correspondence.IgnoreReservation = ignoreReservation;

            CorrespondenceNotificationEntity? captured = null;
            _mockCorrespondenceNotificationRepository
                .Setup(x => x.AddNotification(It.IsAny<CorrespondenceNotificationEntity>(), It.IsAny<CancellationToken>()))
                .Callback<CorrespondenceNotificationEntity, CancellationToken>((n, _) => captured = n)
                .ReturnsAsync(Guid.NewGuid());

            await _handler.Process(request, CancellationToken.None);

            Assert.NotNull(captured);
            var deserialized = JsonSerializer.Deserialize<NotificationOrderRequestV2>(captured!.OrderRequest!);
            Assert.NotNull(deserialized);
            Assert.NotNull(deserialized!.Recipient.RecipientPerson);
            Assert.Equal(ignoreReservation, deserialized!.Recipient.RecipientPerson!.IgnoreReservation);
        }

        [Theory]
        [InlineData("urn:altinn:person:idporten-email:recipient@example.com")]
        [InlineData("urn:altinn:person:legacy-selfidentified:legacy-user")]
        public async Task Process_ShouldUseRecipientExternalIdentity_ForSelfidentifiedUrnRecipients(string correspondenceRecipient)
        {
            var requestedPublishTime = DateTimeOffset.UtcNow.AddMinutes(10);
            var (request, correspondence, _) = SetupOrderData(requestedPublishTime);
            correspondence.Recipient = correspondenceRecipient;

            CorrespondenceNotificationEntity? captured = null;
            _mockCorrespondenceNotificationRepository
                .Setup(x => x.AddNotification(It.IsAny<CorrespondenceNotificationEntity>(), It.IsAny<CancellationToken>()))
                .Callback<CorrespondenceNotificationEntity, CancellationToken>((n, _) => captured = n)
                .ReturnsAsync(Guid.NewGuid());

            await _handler.Process(request, CancellationToken.None);

            Assert.NotNull(captured);
            var deserialized = JsonSerializer.Deserialize<NotificationOrderRequestV2>(captured!.OrderRequest!);
            Assert.NotNull(deserialized);
            Assert.NotNull(deserialized!.Recipient.RecipientExternalIdentity);
            Assert.Equal(correspondenceRecipient, deserialized.Recipient.RecipientExternalIdentity!.ExternalIdentity);
            Assert.NotNull(deserialized.Recipient.RecipientExternalIdentity.ResourceId);
            Assert.Null(deserialized.Recipient.RecipientEmail);
            Assert.Null(deserialized.Recipient.RecipientOrganization);
            Assert.Null(deserialized.Recipient.RecipientPerson);
        }

        [Fact]
        public async Task Process_ShouldSkipPersist_WhenIdempotencyKeyExists()
        {
            // Arrange
            var requestedPublishTime = DateTimeOffset.UtcNow.AddMinutes(10);
            var (request, _, _) = SetupOrderData(requestedPublishTime);

            var inner = new Exception();
            inner.Data["SqlState"] = "23505";
            var dupEx = new DbUpdateException("duplicate", inner);

            _mockIdempotencyKeyRepository
                .Setup(x => x.CreateAsync(It.IsAny<IdempotencyKeyEntity>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(dupEx);

            // Act
            await _handler.Process(request, CancellationToken.None);

            _mockCorrespondenceNotificationRepository.Verify(x => x.AddNotification(It.IsAny<CorrespondenceNotificationEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Process_ShouldRemoveCustomEmailRecipient_WhenEmailIsRegisteredOnRecipientOrganization()
        {
            // Arrange
            var requestedPublishTime = DateTimeOffset.UtcNow.AddMinutes(10);
            var (request, correspondence, _) = SetupOrderData(requestedPublishTime);
            correspondence.Recipient = "urn:altinn:organization:identifier-no:991825827";
            request.NotificationRequest.CustomRecipients =
            [
                new Recipient { EmailAddress = "registered@example.com" },
                new Recipient { EmailAddress = "other@example.com" }
            ];

            _mockAltinnProfileService
                .Setup(x => x.GetOrganizationNotificationAddresses(It.Is<List<string>>(orgs => orgs.Contains("991825827")), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OrgNotificationAddresses>
                {
                    new OrgNotificationAddresses
                    {
                        OrganizationNumber = "991825827",
                        EmailList = ["Registered@Example.com"]
                    }
                });

            var captured = new List<CorrespondenceNotificationEntity>();
            _mockCorrespondenceNotificationRepository
                .Setup(x => x.AddNotification(It.IsAny<CorrespondenceNotificationEntity>(), It.IsAny<CancellationToken>()))
                .Callback<CorrespondenceNotificationEntity, CancellationToken>((n, _) => captured.Add(n))
                .ReturnsAsync(Guid.NewGuid());

            // Act
            await _handler.Process(request, CancellationToken.None);

            // Assert: the custom recipient matching a registered address is removed, the other is kept
            var orders = captured.Select(n => JsonSerializer.Deserialize<NotificationOrderRequestV2>(n.OrderRequest!)!).ToList();
            Assert.Equal(2, orders.Count);
            Assert.Contains(orders, o => o.Recipient.RecipientOrganization != null);
            Assert.Contains(orders, o => o.Recipient.RecipientEmail?.EmailAddress == "other@example.com");
            Assert.DoesNotContain(orders, o => o.Recipient.RecipientEmail?.EmailAddress == "registered@example.com");
        }

        [Fact]
        public async Task Process_ShouldRemoveCustomSmsRecipient_WhenMobileNumberIsRegisteredByUserOnRecipientOrganization()
        {
            // Arrange
            var requestedPublishTime = DateTimeOffset.UtcNow.AddMinutes(10);
            var (request, correspondence, _) = SetupOrderData(requestedPublishTime);
            correspondence.Recipient = "urn:altinn:organization:identifier-no:991825827";
            request.NotificationRequest.CustomRecipients =
            [
                new Recipient { MobileNumber = "+4799999999" },
                new Recipient { MobileNumber = "+4791111111" },
                new Recipient { EmailAddress = "user@example.com" }
            ];

            _mockAltinnProfileService
                .Setup(x => x.GetUserRegisteredContactPoints(It.Is<List<string>>(orgs => orgs.Contains("991825827")), correspondence.ResourceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<UnitContactPoints>
                {
                    new UnitContactPoints
                    {
                        OrganizationNumber = "991825827",
                        UserContactPoints =
                        [
                            new UserRegisteredContactPoint { Email = "user@example.com", MobileNumber = "99999999" }
                        ]
                    }
                });

            var captured = new List<CorrespondenceNotificationEntity>();
            _mockCorrespondenceNotificationRepository
                .Setup(x => x.AddNotification(It.IsAny<CorrespondenceNotificationEntity>(), It.IsAny<CancellationToken>()))
                .Callback<CorrespondenceNotificationEntity, CancellationToken>((n, _) => captured.Add(n))
                .ReturnsAsync(Guid.NewGuid());

            // Act
            await _handler.Process(request, CancellationToken.None);

            // Assert: registered mobile number (without country code) matches custom recipient with country code
            var orders = captured.Select(n => JsonSerializer.Deserialize<NotificationOrderRequestV2>(n.OrderRequest!)!).ToList();
            Assert.Equal(2, orders.Count);
            Assert.Contains(orders, o => o.Recipient.RecipientOrganization != null);
            Assert.Contains(orders, o => o.Recipient.RecipientSms?.PhoneNumber == "+4791111111");
            Assert.DoesNotContain(orders, o => o.Recipient.RecipientSms?.PhoneNumber == "+4799999999");
            Assert.DoesNotContain(orders, o => o.Recipient.RecipientEmail != null);
        }

        [Fact]
        public async Task Process_ShouldKeepCustomRecipients_WhenAddressesNotRegisteredOnRecipientOrganization()
        {
            // Arrange
            var requestedPublishTime = DateTimeOffset.UtcNow.AddMinutes(10);
            var (request, correspondence, _) = SetupOrderData(requestedPublishTime);
            correspondence.Recipient = "urn:altinn:organization:identifier-no:991825827";
            request.NotificationRequest.CustomRecipients =
            [
                new Recipient { EmailAddress = "other@example.com" },
                new Recipient { MobileNumber = "+4791111111" }
            ];

            _mockAltinnProfileService
                .Setup(x => x.GetOrganizationNotificationAddresses(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OrgNotificationAddresses>
                {
                    new OrgNotificationAddresses
                    {
                        OrganizationNumber = "991825827",
                        EmailList = ["registered@example.com"],
                        MobileNumberList = ["+4799999999"]
                    }
                });

            // Act
            await _handler.Process(request, CancellationToken.None);

            // Assert: all three recipients (organization + two custom) get a notification order
            _mockCorrespondenceNotificationRepository.Verify(
                x => x.AddNotification(It.IsAny<CorrespondenceNotificationEntity>(), It.IsAny<CancellationToken>()),
                Times.Exactly(3));
        }

        [Fact]
        public async Task Process_ShouldNotLookUpRegisteredContactInformation_WhenOverrideRegisteredContactInformationIsTrue()
        {
            // Arrange
            var requestedPublishTime = DateTimeOffset.UtcNow.AddMinutes(10);
            var (request, correspondence, _) = SetupOrderData(requestedPublishTime);
            correspondence.Recipient = "urn:altinn:organization:identifier-no:991825827";
            request.NotificationRequest.OverrideRegisteredContactInformation = true;
            request.NotificationRequest.CustomRecipients =
            [
                new Recipient { EmailAddress = "registered@example.com" }
            ];

            // Act
            await _handler.Process(request, CancellationToken.None);

            // Assert: no lookup since the recipient's registered contact information will not be notified
            _mockAltinnProfileService.Verify(
                x => x.GetOrganizationNotificationAddresses(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()),
                Times.Never);
            _mockAltinnProfileService.Verify(
                x => x.GetUserRegisteredContactPoints(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
            _mockCorrespondenceNotificationRepository.Verify(
                x => x.AddNotification(It.IsAny<CorrespondenceNotificationEntity>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Process_ShouldNotLookUpRegisteredContactInformation_WhenRecipientIsPerson()
        {
            // Arrange
            var requestedPublishTime = DateTimeOffset.UtcNow.AddMinutes(10);
            var (request, _, _) = SetupOrderData(requestedPublishTime);
            request.NotificationRequest.CustomRecipients =
            [
                new Recipient { EmailAddress = "other@example.com" }
            ];

            // Act
            await _handler.Process(request, CancellationToken.None);

            // Assert: the lookup only covers organization recipients
            _mockAltinnProfileService.Verify(
                x => x.GetOrganizationNotificationAddresses(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()),
                Times.Never);
            _mockAltinnProfileService.Verify(
                x => x.GetUserRegisteredContactPoints(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
            _mockCorrespondenceNotificationRepository.Verify(
                x => x.AddNotification(It.IsAny<CorrespondenceNotificationEntity>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task Process_ShouldKeepCustomSmsRecipient_WhenChannelsDoNotNotifyRegisteredMobileNumbers()
        {
            // Arrange
            var requestedPublishTime = DateTimeOffset.UtcNow.AddMinutes(10);
            var (request, correspondence, _) = SetupOrderData(requestedPublishTime);
            correspondence.Recipient = "urn:altinn:organization:identifier-no:991825827";
            request.NotificationRequest.NotificationChannel = NotificationChannel.Email;
            request.NotificationRequest.SendReminder = false;
            request.NotificationRequest.CustomRecipients =
            [
                new Recipient { MobileNumber = "+4799999999" }
            ];

            _mockAltinnProfileService
                .Setup(x => x.GetOrganizationNotificationAddresses(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                [
                    new OrgNotificationAddresses
                    {
                        OrganizationNumber = "991825827",
                        MobileNumberList = ["+4799999999"]
                    }
                ]);

            // Act
            await _handler.Process(request, CancellationToken.None);

            // Assert: the email channel never notifies the registered mobile number, so the custom SMS recipient is kept without any lookup
            _mockAltinnProfileService.Verify(
                x => x.GetOrganizationNotificationAddresses(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()),
                Times.Never);
            _mockCorrespondenceNotificationRepository.Verify(
                x => x.AddNotification(It.IsAny<CorrespondenceNotificationEntity>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task Process_ShouldKeepCustomEmailRecipient_WhenChannelsDoNotNotifyRegisteredEmails()
        {
            // Arrange
            var requestedPublishTime = DateTimeOffset.UtcNow.AddMinutes(10);
            var (request, correspondence, _) = SetupOrderData(requestedPublishTime);
            correspondence.Recipient = "urn:altinn:organization:identifier-no:991825827";
            request.NotificationRequest.NotificationChannel = NotificationChannel.Sms;
            request.NotificationRequest.SendReminder = false;
            request.NotificationRequest.CustomRecipients =
            [
                new Recipient { EmailAddress = "registered@example.com" }
            ];

            _mockAltinnProfileService
                .Setup(x => x.GetOrganizationNotificationAddresses(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                [
                    new OrgNotificationAddresses
                    {
                        OrganizationNumber = "991825827",
                        EmailList = ["registered@example.com"]
                    }
                ]);

            // Act
            await _handler.Process(request, CancellationToken.None);

            // Assert: the SMS channel never notifies the registered email, so the custom email recipient is kept without any lookup
            _mockAltinnProfileService.Verify(
                x => x.GetOrganizationNotificationAddresses(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()),
                Times.Never);
            _mockCorrespondenceNotificationRepository.Verify(
                x => x.AddNotification(It.IsAny<CorrespondenceNotificationEntity>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task Process_ShouldRemoveCustomSmsRecipient_WhenReminderChannelNotifiesRegisteredMobileNumbers()
        {
            // Arrange
            var requestedPublishTime = DateTimeOffset.UtcNow.AddMinutes(10);
            var (request, correspondence, _) = SetupOrderData(requestedPublishTime);
            correspondence.Recipient = "urn:altinn:organization:identifier-no:991825827";
            request.NotificationRequest.NotificationChannel = NotificationChannel.Email;
            request.NotificationRequest.SendReminder = true;
            request.NotificationRequest.ReminderNotificationChannel = NotificationChannel.SmsPreferred;
            request.NotificationRequest.CustomRecipients =
            [
                new Recipient { MobileNumber = "+4799999999" }
            ];

            _mockAltinnProfileService
                .Setup(x => x.GetOrganizationNotificationAddresses(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                [
                    new OrgNotificationAddresses
                    {
                        OrganizationNumber = "991825827",
                        MobileNumberList = ["+4799999999"]
                    }
                ]);

            // Act
            await _handler.Process(request, CancellationToken.None);

            // Assert: the reminder channel notifies the registered mobile number, so the custom SMS recipient is removed
            _mockCorrespondenceNotificationRepository.Verify(
                x => x.AddNotification(It.IsAny<CorrespondenceNotificationEntity>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}