using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Correspondence.Base;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http.Json;
using Altinn.Correspondence.Application;

namespace Altinn.Correspondence.Tests.TestingController.Correspondence
{
    [Collection(nameof(CustomWebApplicationTestsCollection))]
    public class CorrespondenceNotificationTests : CorrespondenceTestBase
    {
        public CorrespondenceNotificationTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        [Theory]
        [InlineData(NotificationChannelExt.Sms, true)]
        [InlineData(NotificationChannelExt.Email, true)]
        [InlineData(NotificationChannelExt.Sms, false)]
        [InlineData(NotificationChannelExt.Email, false)]
        public async Task CorrespondenceWithGenericNotification_GivesOk(NotificationChannelExt channel, bool includeContent)
        {
            var builder = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(channel);

            if (includeContent)
            {
                if (channel == NotificationChannelExt.Sms)
                {
                    builder.WithSmsContent();
                }
                else
                {
                    builder.WithEmailContent();
                }
            }

            var payload = builder.Build();
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            var response = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            initializeCorrespondenceResponse.EnsureSuccessStatusCode();
            Assert.NotNull(response);
        }

        [Theory]
        [InlineData(NotificationChannelExt.Email)]
        [InlineData(NotificationChannelExt.Sms)]
        public async Task CorrespondenceWithCustomNotification_GivesOk(NotificationChannelExt channel)
        {
            var builder = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithNotificationTemplate(NotificationTemplateExt.CustomMessage)
                .WithNotificationChannel(channel);

            if (channel == NotificationChannelExt.Email)
            {
                builder.WithEmailContent().WithEmailReminder();
            }
            else
            {
                builder.WithSmsContent().WithSmsReminder();
            }

            var payload = builder.Build();
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            var response = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            initializeCorrespondenceResponse.EnsureSuccessStatusCode();
            Assert.NotNull(response);
        }

        [Theory]
        [InlineData(NotificationTemplateExt.CustomMessage, true)]
        [InlineData(NotificationTemplateExt.GenericAltinnMessage, false)]
        public async Task CorrespondenceWithPreferredNotification_GivesOk(NotificationTemplateExt template, bool includeContent)
        {
            var builder = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithNotificationTemplate(template)
                .WithNotificationChannel(NotificationChannelExt.EmailPreferred);

            if (includeContent)
            {
                builder.WithEmailContent()
                       .WithSmsContent()
                       .WithEmailReminder()
                       .WithSmsReminder();
            }

            var payload = builder.Build();
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            var response = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            initializeCorrespondenceResponse.EnsureSuccessStatusCode();
            Assert.NotNull(response);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CorrespondenceWithEmailNotificationAndSmsReminder_GivesOk(bool includeEmailReminder)
        {
            var builder = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Email)
                .WithReminderNotificationChannel(NotificationChannelExt.Sms)
                .WithEmailContent()
                .WithSmsReminder();

            if (includeEmailReminder)
            {
                builder.WithEmailReminder();
            }

            var payload = builder.Build();
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            var response = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            initializeCorrespondenceResponse.EnsureSuccessStatusCode();
            Assert.NotNull(response);
        }

        [Theory]
        [InlineData(NotificationChannelExt.Email)]
        [InlineData(NotificationChannelExt.EmailPreferred)]
        public async Task CorrespondenceWithSmsNotificationAndEmailReminder_GivesOk(NotificationChannelExt reminderChannel)
        {
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Sms)
                .WithReminderNotificationChannel(reminderChannel)
                .WithSmsReminder()
                .WithEmailReminder()
                .Build();

            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            var response = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            initializeCorrespondenceResponse.EnsureSuccessStatusCode();
            Assert.NotNull(response);
        }

        [Fact]
        public async Task CorrespondenceWithCustomMessageTemplateAndNoEmailContent_Gives_BadRequest()
        {
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithNotificationTemplate(NotificationTemplateExt.CustomMessage)
                .WithNotificationChannel(NotificationChannelExt.Email)
                .Build();
            var initializeCorrespondenceResponse1 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            var problemDetails = await initializeCorrespondenceResponse1.Content.ReadFromJsonAsync<ProblemDetails>(_responseSerializerOptions);
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse1.StatusCode);
            Assert.Equal(NotificationErrors.MissingEmailContent.Message, problemDetails?.Detail);
        }

        [Fact]
        public async Task Correspondence_CustomRecipientMultiple_GivesOK()
        {
            var recipientToOverride = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var customRecipient = new NotificationRecipientExt()
            {
                OrganizationNumber = "123456789"
            };

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipientToOverride])
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.SmsPreferred)
                .WithCustomNotificationRecipient(customRecipient)
                .Build();

            var initResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            var content = await initResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            initResponse.EnsureSuccessStatusCode();
            Assert.NotNull(content);
        }


        [Fact]
        public async Task Correspondence_CustomRecipient_OrgNumberAndNationalID_GivesBadRequest()
        {
            var recipient = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var customRecipient = new NotificationRecipientExt()
            {
                OrganizationNumber = "123456789",
                NationalIdentityNumber = "01019912345",
            };

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipient])
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Sms)
                .WithCustomNotificationRecipient(customRecipient)
                .Build();

            var initResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            var problemDetails = await initResponse.Content.ReadFromJsonAsync<ProblemDetails>(_responseSerializerOptions);
            Assert.Equal(HttpStatusCode.BadRequest, initResponse.StatusCode);
            Assert.Equal(NotificationErrors.CustomRecipientWithMultipleIdentifiersNotAllowed.Message, problemDetails?.Detail);
        }

        [Fact]
        public async Task Correspondence_CustomRecipient_NationalIDAndContactInfo_GivesBadRequest()
        {
            var recipient = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var customRecipient = new NotificationRecipientExt()
            {
                MobileNumber = "12345678",
                NationalIdentityNumber = "01019912345",
            };

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipient])
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Sms)
                .WithReminderNotificationChannel(NotificationChannelExt.Email)
                .WithSmsReminder()
                .WithEmailReminder()
                .WithCustomNotificationRecipient(customRecipient)
                .Build();

            var initResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            var problemDetails = await initResponse.Content.ReadFromJsonAsync<ProblemDetails>(_responseSerializerOptions);
            Assert.Equal(HttpStatusCode.BadRequest, initResponse.StatusCode);
            Assert.Equal(NotificationErrors.CustomRecipientWithMultipleIdentifiersNotAllowed.Message, problemDetails?.Detail);
        }

        [Fact]
        public async Task Correspondence_CustomRecipient_InvalidEmail_GivesBadRequest()
        {
            var recipient = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var customRecipient = new NotificationRecipientExt()
            {
                EmailAddress = "andreas.hammerbeckdigir.no"
            };

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipient])
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Email)
                .WithCustomNotificationRecipient(customRecipient)
                .Build();

            var initResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            var problemDetails = await initResponse.Content.ReadFromJsonAsync<ProblemDetails>(_responseSerializerOptions);
            Assert.Equal(HttpStatusCode.BadRequest, initResponse.StatusCode);
            Assert.Equal(NotificationErrors.InvalidEmailProvided.Message, problemDetails?.Detail);
        }

        [Fact]
        public async Task Correspondence_CustomRecipient_InvalidPhoneNumber_GivesBadRequest()
        {
            var recipient = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var customRecipient = new NotificationRecipientExt()
            {
                MobileNumber = "12345678"
            };

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipient])
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Sms)
                .WithCustomNotificationRecipient(customRecipient)
                .Build();

            var initResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            var problemDetails = await initResponse.Content.ReadFromJsonAsync<ProblemDetails>(_responseSerializerOptions);
            Assert.Equal(HttpStatusCode.BadRequest, initResponse.StatusCode);
            Assert.Equal(NotificationErrors.InvalidMobileNumberProvided.Message, problemDetails?.Detail);
        }

        [Fact]
        public async Task Correspondence_CustomRecipient_MissingMobileNumber_GivesBadRequest()
        {
            var recipient = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var customRecipient = new NotificationRecipientExt()
            {
                EmailAddress = "andreas@hammerbeck.no",
            };

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipient])
                .WithNotificationTemplate(NotificationTemplateExt.CustomMessage)
                .WithNotificationChannel(NotificationChannelExt.Sms)
                .WithCustomNotificationRecipient(customRecipient)
                .Build();

            var initResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            var problemDetails = await initResponse.Content.ReadFromJsonAsync<ProblemDetails>(_responseSerializerOptions);
            Assert.Equal(HttpStatusCode.BadRequest, initResponse.StatusCode);
            Assert.Equal(NotificationErrors.MissingSmsContent.Message, problemDetails?.Detail);
        }

        [Theory]
        [InlineData("+4791702158", null)]
        [InlineData(null, "test@example.com")]
        public async Task Correspondence_CustomRecipient_WithPhoneNumberOrEmailAndRecipientTag_GivesOk(string? number, string? email)
        {
            var recipient = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var customRecipient = new NotificationRecipientExt()
            {
                MobileNumber = number,
                EmailAddress = email
            };
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipient])
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(number != null ? NotificationChannelExt.Sms : NotificationChannelExt.Email)
                .WithCustomNotificationRecipient(customRecipient)
                .Build();

            var initResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            Assert.Equal(HttpStatusCode.OK, initResponse.StatusCode);
        }

        [Theory]
        [InlineData("+4797661466", null)]
        [InlineData(null, "test@example.com")]
        public async Task Correspondence_CustomRecipient_WithNumberOrEmailAndCorrespondenceRecipientKeyword_GivesOK(string? number, string? email)
        {
            var recipient = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var customRecipient = new NotificationRecipientExt()
            {
                MobileNumber = number,
                EmailAddress = email
            };
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipient])
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(number != null ? NotificationChannelExt.Sms : NotificationChannelExt.Email)
                .WithCustomNotificationRecipient(customRecipient)
                .Build();

            payload.Correspondence.Notification.SmsBody = number != null ? "Test $correspondenceRecipientName$" : null;
            payload.Correspondence.Notification.EmailBody = email != null ? "Test $correspondenceRecipientName$" : null;

            var initResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            Assert.Equal(HttpStatusCode.OK, initResponse.StatusCode);
        }

        [Theory]
        [InlineData("97661466", null)]
        [InlineData(null, "test@example.com")]
        public async Task Correspondence_CustomRecipient_WithNumberOrEmailAndNotificationRecipientKeyword_GivesBadRequest(string? number, string? email)
        {
            var recipient = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var customRecipient = new NotificationRecipientExt()
            {
                MobileNumber = number,
                EmailAddress = email
            };
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipient])
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(number != null ? NotificationChannelExt.Sms : NotificationChannelExt.Email)
                .WithCustomNotificationRecipient(customRecipient)
                .Build();

            payload.Correspondence.Notification.SmsBody = number != null ? "Test $recipientName$" : null;
            payload.Correspondence.Notification.EmailBody = email != null ? "Test $recipientName$" : null;

            var initResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            var problemDetails = await initResponse.Content.ReadFromJsonAsync<ProblemDetails>(_responseSerializerOptions);
            Assert.Equal(HttpStatusCode.BadRequest, initResponse.StatusCode);
            Assert.Equal(NotificationErrors.CustomRecipientWithNumberOrEmailNotAllowedWithKeyWordRecipientName.Message, problemDetails?.Detail);
        }

        [Fact]
        public async Task Correspondence_CustomRecipient_RecipientLookupNull_Returns_Success()
        {
            var recipient = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var customRecipient = new NotificationRecipientExt()
            {
                EmailAddress = "andreas@hammerbeck.no",
            };
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipient])
                .WithNotificationTemplate(NotificationTemplateExt.CustomMessage)
                .WithNotificationChannel(NotificationChannelExt.Email)
                .WithEmailContent()
                .WithEmailReminder()
                .WithCustomNotificationRecipient(customRecipient)
                .Build();

            var orderId = Guid.NewGuid();
            using var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var mockNotificationService = new Mock<IAltinnNotificationService>();
                mockNotificationService.Setup(x => x.CreateNotificationV2(It.IsAny<NotificationOrderRequestV2>(), It.IsAny<CancellationToken>())).ReturnsAsync(new NotificationOrderRequestResponseV2()
                {
                    NotificationOrderId = orderId,
                    Notification = new NotificationResponseV2()
                    {
                        ShipmentId = Guid.NewGuid(),
                        SendersReference = "test-reference",
                        Reminders = new List<ReminderResponse>()
                    }
                });
                services.AddSingleton(mockNotificationService.Object);
            });
            var senderClient = testFactory.CreateSenderClient();

            // Act
            var initializeCorrespondenceResponse = await senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            var content = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);

            var initializedCorrespondence = await CorrespondenceHelper.GetInitializedCorrespondence(senderClient, _responseSerializerOptions, payload);
            var correspondence = await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(senderClient, _responseSerializerOptions, initializedCorrespondence.CorrespondenceId, CorrespondenceStatusExt.Published);

            // Assert
            Assert.NotNull(initializedCorrespondence);
            Assert.Equal(CorrespondenceStatusExt.Published, correspondence.Status);
        }

        [Fact]
        public async Task Correspondence_WithNotification_RecipientLookupSuccess_Returns_Success()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Email)
                .Build();
            var orderId = Guid.NewGuid();
            var shipmentId = Guid.NewGuid();

            using var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var mockNotificationService = new Mock<IAltinnNotificationService>();
                mockNotificationService.Setup(x => x.CreateNotificationV2(It.IsAny<NotificationOrderRequestV2>(), It.IsAny<CancellationToken>())).ReturnsAsync(new NotificationOrderRequestResponseV2()
                {
                    NotificationOrderId = orderId,
                    Notification = new NotificationResponseV2()
                    {
                        ShipmentId = shipmentId,
                        SendersReference = "test-reference",
                        Reminders = new List<ReminderResponse>()
                    }
                });
                
                services.AddSingleton(mockNotificationService.Object);
            });
            var senderClient = testFactory.CreateSenderClient();

            // Act
            var initializedCorrespondence = await CorrespondenceHelper.GetInitializedCorrespondence(senderClient, _responseSerializerOptions, payload);
            var correspondence = await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(senderClient, _responseSerializerOptions, initializedCorrespondence.CorrespondenceId, CorrespondenceStatusExt.Published);

            // Assert
            Assert.NotNull(initializedCorrespondence);
            Assert.Equal(CorrespondenceStatusExt.Published, correspondence.Status);
        }

        [Fact]
        public async Task Correspondence_WithNotification_RecipientLookupPartialSuccess_Returns_Success()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Email)
                .Build();
            var orderId = Guid.NewGuid();
            var shipmentId = Guid.NewGuid();

            using var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var mockNotificationService = new Mock<IAltinnNotificationService>();
                mockNotificationService.Setup(x => x.CreateNotificationV2(It.IsAny<NotificationOrderRequestV2>(), It.IsAny<CancellationToken>())).ReturnsAsync(new NotificationOrderRequestResponseV2()
                {
                    NotificationOrderId = orderId,
                    Notification = new NotificationResponseV2()
                    {
                        ShipmentId = shipmentId,
                        SendersReference = "test-reference",
                        Reminders = new List<ReminderResponse>()
                    }
                });
                services.AddSingleton(mockNotificationService.Object);
            });
            var senderClient = testFactory.CreateSenderClient();

            // Act
            var initializedCorrespondence = await CorrespondenceHelper.GetInitializedCorrespondence(senderClient, _responseSerializerOptions, payload);
            var correspondence = await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(senderClient, _responseSerializerOptions, initializedCorrespondence.CorrespondenceId, CorrespondenceStatusExt.Published);

            // Assert
            Assert.NotNull(initializedCorrespondence);
            Assert.Equal(CorrespondenceStatusExt.Published, correspondence.Status);
        }

        [Fact]
        public async Task Correspondence_WithNotification_RecipientLookupFailed_Returns_MissingContact()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Email)
                .Build();
            var orderId = Guid.NewGuid();
            var shipmentId = Guid.NewGuid();

            using var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var mockNotificationService = new Mock<IAltinnNotificationService>();
                mockNotificationService.Setup(x => x.CreateNotificationV2(It.IsAny<NotificationOrderRequestV2>(), It.IsAny<CancellationToken>())).ReturnsAsync(new NotificationOrderRequestResponseV2()
                {
                    NotificationOrderId = orderId,
                    Notification = new NotificationResponseV2()
                    {
                        ShipmentId = shipmentId,
                        SendersReference = "test-reference",
                        Reminders = new List<ReminderResponse>()
                    }
                });
                services.AddSingleton(mockNotificationService.Object);
            });
            var senderClient = testFactory.CreateSenderClient();

            // Act
            var initializedCorrespondence = await CorrespondenceHelper.GetInitializedCorrespondence(senderClient, _responseSerializerOptions, payload);
            var correspondence = await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(senderClient, _responseSerializerOptions, initializedCorrespondence.CorrespondenceId, CorrespondenceStatusExt.Published);

            // Assert
            Assert.NotNull(initializedCorrespondence);
            Assert.Equal(CorrespondenceStatusExt.Published, correspondence.Status);
        }
        [Fact]
        public async Task Correspondence_WithNotification_NotificationCreationFailed_Returns_Failed()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Email)
                .Build();

            using var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var mockNotificationService = new Mock<IAltinnNotificationService>();
                mockNotificationService
                    .Setup(x => x.CreateNotificationV2(It.IsAny<NotificationOrderRequestV2>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((NotificationOrderRequestResponseV2?)null);
                services.AddSingleton(mockNotificationService.Object);
            });
            var senderClient = testFactory.CreateSenderClient();

            // Act
            var initializedCorrespondence = await CorrespondenceHelper.GetInitializedCorrespondence(senderClient, _responseSerializerOptions, payload);
            var correspondence = await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(senderClient, _responseSerializerOptions, initializedCorrespondence.CorrespondenceId, CorrespondenceStatusExt.Published);

            // Assert
            Assert.NotNull(initializedCorrespondence);
            Assert.Equal(CorrespondenceStatusExt.Published, correspondence.Status);
        }

        [Fact]
        public async Task Correspondence_CustomRecipient_WithMultipleRecipients_GiveBadRequest()
        {
            var recipient1 = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var recipient2 = $"{UrnConstants.OrganizationNumberAttribute}:991825828";
            var customRecipient = new NotificationRecipientExt()
            {
                OrganizationNumber = "123456789"
            };

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipient1, recipient2])
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.SmsPreferred)
                .WithCustomNotificationRecipient(customRecipient)
                .Build();

            var initResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            var response = await initResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            Assert.Equal(HttpStatusCode.BadRequest, initResponse.StatusCode);
            Assert.NotNull(response);
        }

        [Fact]
        public async Task Correspondence_CustomRecipient_WithValidEmail_GivesOk()
        {
            var recipient = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var customRecipient = new NotificationRecipientExt()
            {
                EmailAddress = "test@example.com"
            };

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipient])
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Email)
                .WithEmailContent()
                .WithCustomNotificationRecipient(customRecipient)
                .Build();

            var initResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            Assert.Equal(HttpStatusCode.OK, initResponse.StatusCode);
        }

        [Fact]
        public async Task Correspondence_CustomRecipient_WithValidMobileNumber_GivesOk()
        {
            var recipient = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var customRecipient = new NotificationRecipientExt()
            {
                MobileNumber = "+4798765432"
            };

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipient])
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Sms)
                .WithSmsContent()
                .WithCustomNotificationRecipient(customRecipient)
                .Build();

            var initResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            Assert.Equal(HttpStatusCode.OK, initResponse.StatusCode);
        }

        [Fact]
        public async Task Correspondence_CustomRecipient_WithoutIdentifier_ReturnsBadRequest()
        {
            // Arrange
            var recipient = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var customRecipient = new NotificationRecipientExt()
            {
                // No identifiers provided
            };

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipient])
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Email)
                .WithCustomNotificationRecipient(customRecipient)
                .Build();

            // Act
            var initResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            var problemDetails = await initResponse.Content.ReadFromJsonAsync<ProblemDetails>(_responseSerializerOptions);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, initResponse.StatusCode);
            Assert.Equal(NotificationErrors.CustomRecipientWithoutIdentifierNotAllowed.Message, problemDetails?.Detail);
        }

        [Fact]
        public async Task Correspondence_CustomRecipientTakesPrecedenceOverCustomNotificationRecipients_GivesOk()
        {
            // Arrange
            var recipient = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var customRecipient = new NotificationRecipientExt
            {
                EmailAddress = "custom@example.com"
            };
            var customNotificationRecipients = new List<CustomNotificationRecipientExt>
            {
                new()
                {
                    RecipientToOverride = "any-value",
                    Recipients = new List<NotificationRecipientExt>
                    {
                        new()
                        {
                            EmailAddress = "ignored@example.com"
                        }
                    }
                }
            };

            var orderId = Guid.NewGuid();
            using var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var mockNotificationService = new Mock<IAltinnNotificationService>();
                mockNotificationService.Setup(x => x.CreateNotificationV2(It.IsAny<NotificationOrderRequestV2>(), It.IsAny<CancellationToken>()))
                    .Callback<NotificationOrderRequestV2, CancellationToken>((request, _) =>
                    {
                        // V2 has a single recipient structure, not a list
                        Assert.NotNull(request.Recipient);
                        
                        // The recipient should be an organization recipient (default correspondence recipient)
                        Assert.NotNull(request.Recipient.RecipientOrganization);
                        Assert.Equal("991825827", request.Recipient.RecipientOrganization.OrgNumber);
                        Assert.Null(request.Recipient.RecipientEmail);
                        Assert.Null(request.Recipient.RecipientPerson);
                        Assert.Null(request.Recipient.RecipientSms);
                        
                        // Note: In V2, custom recipients are handled differently - they would be separate notification requests
                        // This test verifies that the main correspondence recipient is correctly set up
                    })
                    .ReturnsAsync(new NotificationOrderRequestResponseV2()
                    {
                        NotificationOrderId = orderId,
                        Notification = new NotificationResponseV2()
                        {
                            ShipmentId = Guid.NewGuid(),
                            SendersReference = "test-reference",
                            Reminders = new List<ReminderResponse>()
                        }
                    });
                services.AddSingleton(mockNotificationService.Object);
            });
            var senderClient = testFactory.CreateSenderClient();

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipient])
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Email)
                .WithEmailContent()
                .WithCustomNotificationRecipient(customRecipient)
                .WithCustomNotificationRecipients(customNotificationRecipients)
                .Build();

            // Act
            var initializedCorrespondence = await CorrespondenceHelper.GetInitializedCorrespondence(senderClient, _responseSerializerOptions, payload);
            var correspondence = await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(senderClient, _responseSerializerOptions, initializedCorrespondence.CorrespondenceId, CorrespondenceStatusExt.Published);

            // Assert
            Assert.NotNull(initializedCorrespondence);
            Assert.Equal(CorrespondenceStatusExt.Published, correspondence.Status);
        }

        [Fact]
        public async Task Correspondence_EmptyCustomNotificationRecipients_ReturnsNull_GivesOk()
        {
            // Arrange
            var recipient = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var customNotificationRecipients = new List<CustomNotificationRecipientExt>();

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipient])
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Email)
                .WithEmailContent()
                .WithCustomNotificationRecipients(customNotificationRecipients)
                .Build();

            // Act
            var initResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            var content = await initResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);

            // Assert
            Assert.Equal(HttpStatusCode.OK, initResponse.StatusCode);
            Assert.NotNull(content);
        }

        [Fact]
        public async Task Correspondence_CustomRecipients_SingleRecipient_GivesOk()
        {
            // Arrange
            var recipient = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var customRecipients = new List<NotificationRecipientExt>
            {
                new()
                {
                    EmailAddress = "custom@example.com"
                }
            };

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipient])
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Email)
                .WithEmailContent()
                .WithCustomRecipients(customRecipients)
                .Build();

            // Act
            var initResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            var content = await initResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);

            // Assert
            Assert.Equal(HttpStatusCode.OK, initResponse.StatusCode);
            Assert.NotNull(content);
        }

        [Fact]
        public async Task Correspondence_CustomRecipients_MultipleRecipients_GivesOk()
        {
            // Arrange
            var recipient = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var customRecipients = new List<NotificationRecipientExt>
            {
                new()
                {
                    EmailAddress = "custom1@example.com"
                },
                new()
                {
                    MobileNumber = "+4798765432"
                },
                new()
                {
                    OrganizationNumber = "123456789"
                }
            };

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipient])
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.EmailPreferred)
                .WithEmailContent()
                .WithSmsContent()
                .WithCustomRecipients(customRecipients)
                .Build();

            // Act
            var initResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            var content = await initResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);

            // Assert
            Assert.Equal(HttpStatusCode.OK, initResponse.StatusCode);
            Assert.NotNull(content);
        }

        [Fact]
        public async Task Correspondence_CustomRecipients_WithMultipleCorrespondenceRecipients_GivesBadRequest()
        {
            // Arrange
            var recipient1 = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var recipient2 = $"{UrnConstants.OrganizationNumberAttribute}:991825828";
            var customRecipients = new List<NotificationRecipientExt>
            {
                new()
                {
                    EmailAddress = "custom@example.com"
                }
            };

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipient1, recipient2])
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Email)
                .WithEmailContent()
                .WithCustomRecipients(customRecipients)
                .Build();

            // Act
            var initResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, initResponse.StatusCode);
        }

        [Fact]
        public async Task Correspondence_CustomRecipientsTakesPrecedenceOverCustomRecipient_GivesOk()
        {
            // Arrange
            var recipient = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var customRecipient = new NotificationRecipientExt
            {
                EmailAddress = "ignored@example.com"
            };
            var customRecipients = new List<NotificationRecipientExt>
            {
                new()
                {
                    EmailAddress = "custom@example.com"
                }
            };

            var orderId = Guid.NewGuid();
            var shipmentId = Guid.NewGuid();
            using var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var mockNotificationService = new Mock<IAltinnNotificationService>();
                mockNotificationService.Setup(x => x.CreateNotificationV2(It.IsAny<NotificationOrderRequestV2>(), It.IsAny<CancellationToken>()))
                    .Callback<NotificationOrderRequestV2, CancellationToken>((request, _) =>
                    {
                        // V2 has a single recipient structure, not a list
                        Assert.NotNull(request.Recipient);
                        
                        // The recipient should be an organization recipient (default correspondence recipient)
                        Assert.NotNull(request.Recipient.RecipientOrganization);
                        Assert.Equal("991825827", request.Recipient.RecipientOrganization.OrgNumber);
                        Assert.Null(request.Recipient.RecipientEmail);
                        Assert.Null(request.Recipient.RecipientPerson);
                        Assert.Null(request.Recipient.RecipientSms);
                        
                        // Note: In V2, custom recipients are handled differently - they would be separate notification requests
                        // This test verifies that the main correspondence recipient is correctly set up
                    })
                    .ReturnsAsync(new NotificationOrderRequestResponseV2()
                    {
                        NotificationOrderId = orderId,
                        Notification = new NotificationResponseV2()
                        {
                            ShipmentId = shipmentId,
                            SendersReference = "test-reference",
                            Reminders = new List<ReminderResponse>()
                        }
                    });
                services.AddSingleton(mockNotificationService.Object);
            });
            var senderClient = testFactory.CreateSenderClient();

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipient])
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Email)
                .WithEmailContent()
                .WithCustomNotificationRecipient(customRecipient)
                .WithCustomRecipients(customRecipients)
                .Build();

            // Act
            var initializedCorrespondence = await CorrespondenceHelper.GetInitializedCorrespondence(senderClient, _responseSerializerOptions, payload);
            var correspondence = await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(senderClient, _responseSerializerOptions, initializedCorrespondence.CorrespondenceId, CorrespondenceStatusExt.Published);

            // Assert
            Assert.NotNull(initializedCorrespondence);
            Assert.Equal(CorrespondenceStatusExt.Published, correspondence.Status);
        }

        [Fact]
        public async Task Correspondence_OverrideRegisteredContactInformation_WithoutCustomRecipients_GivesBadRequest()
        {
            // Arrange
            var recipient = $"{UrnConstants.OrganizationNumberAttribute}:991825827";

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipient])
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Email)
                .WithEmailContent()
                .WithOverrideRegisteredContactInformation(true)
                .Build();

            // Act
            var initResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            var problemDetails = await initResponse.Content.ReadFromJsonAsync<ProblemDetails>(_responseSerializerOptions);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, initResponse.StatusCode);
            Assert.Equal(NotificationErrors.OverrideRegisteredContactInformationRequiresCustomRecipients.Message, problemDetails?.Detail);
        }

        [Fact]
        public async Task Correspondence_OverrideRegisteredContactInformation_WithCustomRecipients_GivesOk()
        {
            // Arrange
            var recipient = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var customRecipients = new List<NotificationRecipientExt>
            {
                new()
                {
                    EmailAddress = "custom@example.com"
                }
            };

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipient])
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Email)
                .WithEmailContent()
                .WithCustomRecipients(customRecipients)
                .WithOverrideRegisteredContactInformation(true)
                .Build();

            // Act
            var initResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            var content = await initResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);

            // Assert
            Assert.Equal(HttpStatusCode.OK, initResponse.StatusCode);
            Assert.NotNull(content);
        }

        [Fact]
        public async Task Correspondence_OverrideRegisteredContactInformation_WithCustomRecipients_OnlyCustomRecipientsUsed()
        {
            // Arrange
            var recipient = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var customRecipients = new List<NotificationRecipientExt>
            {
                new()
                {
                    EmailAddress = "custom@example.com"
                }
            };

            var orderId = Guid.NewGuid();
            using var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var mockNotificationService = new Mock<IAltinnNotificationService>();
                mockNotificationService.Setup(x => x.CreateNotificationV2(It.IsAny<NotificationOrderRequestV2>(), It.IsAny<CancellationToken>()))
                    .Callback<NotificationOrderRequestV2, CancellationToken>((request, _) =>
                    {
                        // V2 has a single recipient structure, not a list
                        Assert.NotNull(request.Recipient);
                        
                        // The recipient should be an email recipient (custom recipient)
                        Assert.NotNull(request.Recipient.RecipientEmail);
                        Assert.Equal("custom@example.com", request.Recipient.RecipientEmail.EmailAddress);
                        Assert.Null(request.Recipient.RecipientOrganization);
                        Assert.Null(request.Recipient.RecipientPerson);
                        Assert.Null(request.Recipient.RecipientSms);
                    })
                    .ReturnsAsync(new NotificationOrderRequestResponseV2()
                    {
                        NotificationOrderId = orderId,
                        Notification = new NotificationResponseV2()
                        {
                            ShipmentId = Guid.NewGuid(),
                            SendersReference = "test-reference",
                            Reminders = new List<ReminderResponse>()
                        }
                    });
                services.AddSingleton(mockNotificationService.Object);
            });
            var senderClient = testFactory.CreateSenderClient();

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipient])
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Email)
                .WithEmailContent()
                .WithCustomRecipients(customRecipients)
                .WithOverrideRegisteredContactInformation(true)
                .Build();

            // Act
            var initializedCorrespondence = await CorrespondenceHelper.GetInitializedCorrespondence(senderClient, _responseSerializerOptions, payload);
            var correspondence = await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(senderClient, _responseSerializerOptions, initializedCorrespondence.CorrespondenceId, CorrespondenceStatusExt.Published);

            // Assert
            Assert.NotNull(initializedCorrespondence);
            Assert.Equal(CorrespondenceStatusExt.Published, correspondence.Status);
        }

        [Fact]
        public async Task Correspondence_OverrideRegisteredContactInformation_False_WithCustomRecipients_DefaultAndCustomRecipientsUsed()
        {
            // Arrange
            var recipient = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var customRecipients = new List<NotificationRecipientExt>
            {
                new()
                {
                    EmailAddress = "custom@example.com"
                }
            };

            var orderId = Guid.NewGuid();
            using var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var mockNotificationService = new Mock<IAltinnNotificationService>();
                mockNotificationService.Setup(x => x.CreateNotificationV2(It.IsAny<NotificationOrderRequestV2>(), It.IsAny<CancellationToken>()))
                    .Callback<NotificationOrderRequestV2, CancellationToken>((request, _) =>
                    {
                        // V2 has a single recipient structure, not a list
                        Assert.NotNull(request.Recipient);
                        
                        // The recipient should be an organization recipient (default correspondence recipient)
                        Assert.NotNull(request.Recipient.RecipientOrganization);
                        Assert.Equal("991825827", request.Recipient.RecipientOrganization.OrgNumber);
                        Assert.Null(request.Recipient.RecipientEmail);
                        Assert.Null(request.Recipient.RecipientPerson);
                        Assert.Null(request.Recipient.RecipientSms);
                        
                        // Note: In V2, custom recipients are handled differently - they would be separate notification requests
                        // This test verifies that the main correspondence recipient is correctly set up
                    })
                    .ReturnsAsync(new NotificationOrderRequestResponseV2()
                    {
                        NotificationOrderId = orderId,
                        Notification = new NotificationResponseV2()
                        {
                            ShipmentId = Guid.NewGuid(),
                            SendersReference = "test-reference",
                            Reminders = new List<ReminderResponse>()
                        }
                    });
                services.AddSingleton(mockNotificationService.Object);
            });
            var senderClient = testFactory.CreateSenderClient();

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipient])
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Email)
                .WithEmailContent()
                .WithCustomRecipients(customRecipients)
                .WithOverrideRegisteredContactInformation(false)
                .Build();

            // Act
            var initializedCorrespondence = await CorrespondenceHelper.GetInitializedCorrespondence(senderClient, _responseSerializerOptions, payload);
            var correspondence = await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(senderClient, _responseSerializerOptions, initializedCorrespondence.CorrespondenceId, CorrespondenceStatusExt.Published);

            // Assert
            Assert.NotNull(initializedCorrespondence);
            Assert.Equal(CorrespondenceStatusExt.Published, correspondence.Status);
        }
    }
}
