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

        [Fact]
        public async Task CorrespondenceWithGenericNotification_Gives_Ok()
        {
            var smsPayload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Sms)
                .WithSmsContent()
                .Build();
            var initializeCorrespondenceResponse1 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", smsPayload, _responseSerializerOptions);
            var response1 = await initializeCorrespondenceResponse1.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            initializeCorrespondenceResponse1.EnsureSuccessStatusCode();
            Assert.NotNull(response1);

            var emailPayload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Email)
                .WithEmailContent()
                .Build();
            var initializeCorrespondenceResponse2 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", emailPayload, _responseSerializerOptions);
            var response2 = await initializeCorrespondenceResponse2.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            initializeCorrespondenceResponse2.EnsureSuccessStatusCode();
            Assert.NotNull(response2);

            var emptySmsPayload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Sms)
                .Build();
            var initializeCorrespondenceResponse3 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", emptySmsPayload, _responseSerializerOptions);
            var response3 = await initializeCorrespondenceResponse3.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            initializeCorrespondenceResponse3.EnsureSuccessStatusCode();
            Assert.NotNull(response3);

            var emptyEmailPayload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Email)
                .Build();
            var initializeCorrespondenceResponse4 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", emptyEmailPayload, _responseSerializerOptions);
            var response4 = await initializeCorrespondenceResponse4.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            initializeCorrespondenceResponse4.EnsureSuccessStatusCode();
            Assert.NotNull(response4);
        }

        [Fact]
        public async Task CorrespondenceWithCustomNotification_Gives_Ok()
        {
            var emailPayload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithNotificationTemplate(NotificationTemplateExt.CustomMessage)
                .WithNotificationChannel(NotificationChannelExt.Email)
                .WithEmailContent()
                .WithEmailReminder()
                .Build();
            var initializeCorrespondenceResponse1 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", emailPayload, _responseSerializerOptions);
            var response1 = await initializeCorrespondenceResponse1.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            initializeCorrespondenceResponse1.EnsureSuccessStatusCode();
            Assert.NotNull(response1);

            var smsPayload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithNotificationTemplate(NotificationTemplateExt.CustomMessage)
                .WithNotificationChannel(NotificationChannelExt.Sms)
                .WithSmsContent()
                .WithSmsReminder()
                .Build();
            var initializeCorrespondenceResponse2 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", smsPayload, _responseSerializerOptions);
            var response2 = await initializeCorrespondenceResponse2.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            initializeCorrespondenceResponse2.EnsureSuccessStatusCode();
            Assert.NotNull(response2);
        }

        [Fact]
        public async Task CorrespondenceWithPreferredNotification_Gives_Ok()
        {
            var preferredEmailCustomPayload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithNotificationTemplate(NotificationTemplateExt.CustomMessage)
                .WithNotificationChannel(NotificationChannelExt.EmailPreferred)
                .WithEmailContent()
                .WithSmsContent()
                .WithEmailReminder()
                .WithSmsReminder()
                .Build();
            var initializeCorrespondenceResponse1 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", preferredEmailCustomPayload, _responseSerializerOptions);
            var response1 = await initializeCorrespondenceResponse1.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            initializeCorrespondenceResponse1.EnsureSuccessStatusCode();
            Assert.NotNull(response1);

            var emailPreferredAltinnPayload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.EmailPreferred)
                .Build();
            var initializeCorrespondenceResponse2 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", emailPreferredAltinnPayload, _responseSerializerOptions);
            var response2 = await initializeCorrespondenceResponse2.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            initializeCorrespondenceResponse2.EnsureSuccessStatusCode();
            Assert.NotNull(response2);
        }

        [Fact]
        public async Task CorrespondenceWithEmailNotificationAndSmsReminder_Gives_Ok()
        {
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Email)
                .WithReminderNotificationChannel(NotificationChannelExt.Sms)
                .WithEmailContent()
                .WithSmsReminder()
                .Build();
            var initializeCorrespondenceResponse1 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            var response1 = await initializeCorrespondenceResponse1.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            initializeCorrespondenceResponse1.EnsureSuccessStatusCode();
            Assert.NotNull(response1);

            var payload2 = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Email)
                .WithReminderNotificationChannel(NotificationChannelExt.Sms)
                .WithEmailContent()
                .WithSmsReminder()
                .WithEmailReminder()
                .Build();
            var initializeCorrespondenceResponse2 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload2, _responseSerializerOptions);
            var response2 = await initializeCorrespondenceResponse2.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            initializeCorrespondenceResponse2.EnsureSuccessStatusCode();
            Assert.NotNull(response2);
        }

        [Fact]
        public async Task CorrespondenceWithSmsNotificationAndEmailReminder_Gives_Ok()
        {
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Sms)
                .WithReminderNotificationChannel(NotificationChannelExt.Email)
                .WithSmsReminder()
                .WithEmailReminder()
                .Build();

            var initializeCorrespondenceResponse1 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            var response1 = await initializeCorrespondenceResponse1.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            initializeCorrespondenceResponse1.EnsureSuccessStatusCode();
            Assert.NotNull(response1);

            var payload2 = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Sms)
                .WithReminderNotificationChannel(NotificationChannelExt.EmailPreferred)
                .WithSmsReminder()
                .WithEmailReminder()
                .Build();
            var initializeCorrespondenceResponse2 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload2, _responseSerializerOptions);
            var response2 = await initializeCorrespondenceResponse2.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            initializeCorrespondenceResponse2.EnsureSuccessStatusCode();
            Assert.NotNull(response2);
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
            var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var mockNotificationService = new Mock<IAltinnNotificationService>();
                mockNotificationService.Setup(x => x.CreateNotification(It.IsAny<NotificationOrderRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new NotificationOrderRequestResponse()
                {
                    OrderId = orderId,
                    RecipientLookup = null
                });
                services.AddSingleton(mockNotificationService.Object);
            });
            var senderClient = testFactory.CreateSenderClient();

            // Act
            var initializeCorrespondenceResponse = await senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            var content = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            // Assert
            Assert.Equal(HttpStatusCode.OK, initializeCorrespondenceResponse.StatusCode);
            Assert.Equal(CorrespondenceStatusExt.Published, content?.Correspondences.First().Status);
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

            var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var mockNotificationService = new Mock<IAltinnNotificationService>();
                mockNotificationService.Setup(x => x.CreateNotification(It.IsAny<NotificationOrderRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new NotificationOrderRequestResponse()
                {
                    OrderId = orderId,
                    RecipientLookup = new RecipientLookupResult()
                    {
                        Status = RecipientLookupStatus.Success,
                        MissingContact = [],
                        IsReserved = []
                    }
                });
                
                services.AddSingleton(mockNotificationService.Object);
            });
            var senderClient = testFactory.CreateSenderClient();

            // Act
            var initializeCorrespondenceResponse = await senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            var content = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);

            // Assert
            Assert.Equal(HttpStatusCode.OK, initializeCorrespondenceResponse.StatusCode);
            Assert.Equal(CorrespondenceStatusExt.Published, content?.Correspondences.First().Status);
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

            var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var mockNotificationService = new Mock<IAltinnNotificationService>();
                mockNotificationService.Setup(x => x.CreateNotification(It.IsAny<NotificationOrderRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new NotificationOrderRequestResponse()
                {
                    OrderId = orderId,
                    RecipientLookup = new RecipientLookupResult()
                    {
                        Status = RecipientLookupStatus.PartialSuccess,
                        MissingContact = [],
                        IsReserved = []
                    }
                });
                services.AddSingleton(mockNotificationService.Object);
            });
            var senderClient = testFactory.CreateSenderClient();

            // Act
            var initializeCorrespondenceResponse = await senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            var content = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);

            // Assert
            Assert.Equal(HttpStatusCode.OK, initializeCorrespondenceResponse.StatusCode);
            Assert.Equal(CorrespondenceStatusExt.Published, content?.Correspondences.First().Status);
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

            var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var mockNotificationService = new Mock<IAltinnNotificationService>();
                mockNotificationService.Setup(x => x.CreateNotification(It.IsAny<NotificationOrderRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new NotificationOrderRequestResponse()
                {
                    OrderId = orderId,
                    RecipientLookup = new RecipientLookupResult()
                    {
                        Status = RecipientLookupStatus.Failed,
                        MissingContact = [],
                        IsReserved = []
                    }
                });
                services.AddSingleton(mockNotificationService.Object);
            });
            var senderClient = testFactory.CreateSenderClient();

            // Act
            var initializeCorrespondenceResponse = await senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            var content = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);

            // Assert
            Assert.Equal(HttpStatusCode.OK, initializeCorrespondenceResponse.StatusCode);
            Assert.Equal(CorrespondenceStatusExt.Published, content?.Correspondences.First().Status);
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

            var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var mockNotificationService = new Mock<IAltinnNotificationService>();
                mockNotificationService.Setup(x => x.CreateNotification(It.IsAny<NotificationOrderRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync((NotificationOrderRequestResponse)null);
                services.AddSingleton(mockNotificationService.Object);
            });
            var senderClient = testFactory.CreateSenderClient();

            // Act
            var initializeCorrespondenceResponse = await senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            var content = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);

            // Assert
            Assert.Equal(HttpStatusCode.OK, initializeCorrespondenceResponse.StatusCode);
            Assert.Equal(CorrespondenceStatusExt.Published, content?.Correspondences.First().Status);
        }

        [Fact]
        public async Task Correspondence_CustomRecipient_WithMultipleRecipients_GivesBadRequest()
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
            var responseContent = await initResponse.Content.ReadAsStringAsync();
            Console.WriteLine($"Response content: {responseContent}");
            var problemDetails = await initResponse.Content.ReadFromJsonAsync<ProblemDetails>(_responseSerializerOptions);
            Assert.Equal(HttpStatusCode.BadRequest, initResponse.StatusCode);
            Assert.Equal(NotificationErrors.CustomRecipientWithMultipleRecipientsNotAllowed.Message, problemDetails?.Detail);
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

    }
}
