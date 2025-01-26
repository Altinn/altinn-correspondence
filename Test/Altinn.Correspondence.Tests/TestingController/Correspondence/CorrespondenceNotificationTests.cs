using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Correspondence.Base;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http.Json;

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
        public async Task CorrespondenceWithEmptyCustomNotification_Gives_BadRequest()
        {
            var payload1 = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithNotificationTemplate(NotificationTemplateExt.CustomMessage)
                .WithNotificationChannel(NotificationChannelExt.Email)
                .Build();
            var initializeCorrespondenceResponse1 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload1, _responseSerializerOptions);
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse1.StatusCode);

            var payload2 = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithNotificationTemplate(NotificationTemplateExt.CustomMessage)
                .WithNotificationChannel(NotificationChannelExt.Sms)
                .Build();
            var initializeCorrespondenceResponse2 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload2, _responseSerializerOptions);
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse2.StatusCode);

            var payload3 = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithNotificationTemplate(NotificationTemplateExt.CustomMessage)
                .WithNotificationChannel(NotificationChannelExt.Email)
                .WithoutSendReminder()
                .Build();
            var initializeCorrespondenceResponse3 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload3, _responseSerializerOptions);
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse3.StatusCode);

            var payload4 = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithNotificationTemplate(NotificationTemplateExt.CustomMessage)
                .WithNotificationChannel(NotificationChannelExt.Email)
                .WithEmailContent()
                .Build();
            var initializeCorrespondenceResponse4 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload4, _responseSerializerOptions);
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse4.StatusCode);
        }

        [Fact]
        public async Task Correspondence_CustomRecipient_OneToMany_GivesOK()
        {
            var recipientToOverride = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var customRecipients = new List<CustomNotificationRecipientExt>()
            {
                new CustomNotificationRecipientExt()
                {
                    RecipientToOverride = recipientToOverride,
                    Recipients = new List<NotificationRecipientExt>()
                    {
                        new NotificationRecipientExt()
                        {
                            OrganizationNumber = "123456789",
                        },
                        new NotificationRecipientExt()
                        {
                            MobileNumber = "+4798765432",
                            EmailAddress = "andreas.håmmerbeck@digdær.no"
                        },
                        new NotificationRecipientExt()
                        {
                            MobileNumber = "004798765432",
                            EmailAddress = "andreås.hammerbeck@digdør.no"
                        },
                        new NotificationRecipientExt()
                        {
                            NationalIdentityNumber = "12345678901",
                        }
                    }
                }
            };
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipientToOverride])
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.SmsPreferred)
                .WithCustomNotificationRecipients(customRecipients)
                .Build();

            var initResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            var content = await initResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            initResponse.EnsureSuccessStatusCode();
            Assert.NotNull(content);
        }
        [Fact]
        public async Task Correspondence_CustomRecipientMultiple_GivesOK()
        {
            var recipientToOverride1 = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var recipientToOverride2 = $"{UrnConstants.OrganizationNumberAttribute}:991825828";
            var customRecipients = new List<CustomNotificationRecipientExt>()
            {
                new CustomNotificationRecipientExt()
                {
                    RecipientToOverride = recipientToOverride1,
                    Recipients = new List<NotificationRecipientExt>()
                    {
                        new NotificationRecipientExt()
                        {
                            OrganizationNumber = "123456789",
                        },
                    }
                },
                new CustomNotificationRecipientExt()
                {
                    RecipientToOverride = recipientToOverride2,
                    Recipients = new List<NotificationRecipientExt>()
                    {
                        new NotificationRecipientExt()
                        {
                            NationalIdentityNumber = "12345678901",
                        }
                    }
                }
            };
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipientToOverride1, recipientToOverride2])
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.SmsPreferred)
                .WithCustomNotificationRecipients(customRecipients)
                .Build();

            var initResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            var content = await initResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            initResponse.EnsureSuccessStatusCode();
            Assert.NotNull(content);
        }

        [Fact]
        public async Task Correspondence_CustomRecipient_RecipientToOverride_NotExist_GivesBadRequest()
        {
            var recipient = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var recipientToOverride = $"{UrnConstants.OrganizationNumberAttribute}:123456789";
            var customRecipients = new List<CustomNotificationRecipientExt>()
            {
                new CustomNotificationRecipientExt()
                {
                    RecipientToOverride = recipientToOverride,
                    Recipients = new List<NotificationRecipientExt>()
                    {
                        new NotificationRecipientExt()
                        {
                            NationalIdentityNumber = "12345678901",
                        }
                    }
                }
            };
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipient])
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.SmsPreferred)
                .WithCustomNotificationRecipients(customRecipients)
                .Build();

            var initResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            Assert.Equal(HttpStatusCode.BadRequest, initResponse.StatusCode);
        }

        [Fact]
        public async Task Correspondence_CustomRecipient_OrgNumberAndContactInfo_GivesBadRequest()
        {
            var recipient = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var customRecipients = new List<CustomNotificationRecipientExt>()
            {
                new CustomNotificationRecipientExt()
                {
                    RecipientToOverride = recipient,
                    Recipients = new List<NotificationRecipientExt>()
                    {
                        new NotificationRecipientExt()
                        {
                            OrganizationNumber = "123456789",
                            MobileNumber = "12345678",
                        },
                    }
                }
            };

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipient])
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.SmsPreferred)
                .WithCustomNotificationRecipients(customRecipients)
                .Build();

            var initResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            Assert.Equal(HttpStatusCode.BadRequest, initResponse.StatusCode);
        }

        [Fact]
        public async Task Correspondence_CustomRecipient_OrgNumberAndNationalID_GivesBadRequest()
        {
            var recipient = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var customRecipients = new List<CustomNotificationRecipientExt>()
            {
                new CustomNotificationRecipientExt()
                {
                    RecipientToOverride = recipient,
                    Recipients = new List<NotificationRecipientExt>()
                    {
                        new NotificationRecipientExt()
                        {
                            OrganizationNumber = "123456789",
                            NationalIdentityNumber = "01019912345",
                        },
                    }
                }
            };

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipient])
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.SmsPreferred)
                .WithCustomNotificationRecipients(customRecipients)
                .Build();

            var initResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            Assert.Equal(HttpStatusCode.BadRequest, initResponse.StatusCode);
        }

        [Fact]
        public async Task Correspondence_CustomRecipient_NationalIDAndContactInfo_GivesBadRequest()
        {
            var recipient = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var customRecipients = new List<CustomNotificationRecipientExt>()
            {
                new CustomNotificationRecipientExt()
                {
                    RecipientToOverride = recipient,
                    Recipients = new List<NotificationRecipientExt>()
                    {
                        new NotificationRecipientExt()
                        {
                            MobileNumber = "12345678",
                            NationalIdentityNumber = "01019912345",
                        },
                    }
                }
            };

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipient])
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Sms)
                .WithReminderNotificationChannel(NotificationChannelExt.Email)
                .WithSmsReminder()
                .WithEmailReminder()
                .WithCustomNotificationRecipients(customRecipients)
                .Build();

            var initResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            Assert.Equal(HttpStatusCode.BadRequest, initResponse.StatusCode);
        }

        [Fact]
        public async Task Correspondence_CustomRecipient_MissingEmail_GivesBadRequest()
        {
            var recipient = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var customRecipients = new List<CustomNotificationRecipientExt>()
            {
                new CustomNotificationRecipientExt()
                {
                    RecipientToOverride = recipient,
                    Recipients = new List<NotificationRecipientExt>()
                    {
                        new NotificationRecipientExt()
                        {
                            MobileNumber = "12345678",
                        },
                    }
                }
            };
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipient])
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Email)
                .WithCustomNotificationRecipients(customRecipients)
                .Build();
            
            var initResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            Assert.Equal(HttpStatusCode.BadRequest, initResponse.StatusCode);
        }

        [Fact]
        public async Task Correspondence_CustomRecipient_InvalidEmail_GivesBadRequest()
        {
            var recipient = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var customRecipients = new List<CustomNotificationRecipientExt>()
            {
                new CustomNotificationRecipientExt()
                {
                    RecipientToOverride = recipient,
                    Recipients = new List<NotificationRecipientExt>()
                    {
                        new NotificationRecipientExt()
                        {
                            MobileNumber = "12345678",
                            EmailAddress = "andreas.hammerbeckdigir.no"
                        },
                    }
                }
            };
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipient])
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Email)
                .WithCustomNotificationRecipients(customRecipients)
                .Build();
            
            var initResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            Assert.Equal(HttpStatusCode.BadRequest, initResponse.StatusCode);
        }

        [Fact]
        public async Task Correspondence_CustomRecipient_InvalidPhoneNumber_GivesBadRequest()
        {
            var recipient = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var customRecipients = new List<CustomNotificationRecipientExt>()
            {
                new CustomNotificationRecipientExt()
                {
                    RecipientToOverride = recipient,
                    Recipients = new List<NotificationRecipientExt>()
                    {
                        new NotificationRecipientExt()
                        {
                            MobileNumber = "12345678",
                        }
                    }
                }
            };
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipient])
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Sms)
                .WithCustomNotificationRecipients(customRecipients)
                .Build();
            
            var initResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            Assert.Equal(HttpStatusCode.BadRequest, initResponse.StatusCode);
        }

        [Fact]
        public async Task Correspondence_CustomRecipient_MissingMobileNumber_GivesBadRequest()
        {
            var recipient = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var customRecipients = new List<CustomNotificationRecipientExt>()
            {
                new CustomNotificationRecipientExt()
                {
                    RecipientToOverride = recipient,
                    Recipients = new List<NotificationRecipientExt>()
                    {
                        new NotificationRecipientExt()
                        {
                            EmailAddress = "andreas@hammerbeck.no",
                        }
                    }
                }
            };
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipient])
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Sms)
                .WithCustomNotificationRecipients(customRecipients)
                .Build();
            
            var initResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload, _responseSerializerOptions);
            Assert.Equal(HttpStatusCode.BadRequest, initResponse.StatusCode);
        }

        [Fact]
        public async Task Correspondence_CustomRecipient_RecipientLookupNull_Returns_Success()
        {
            var recipient = $"{UrnConstants.OrganizationNumberAttribute}:991825827";
            var customRecipients = new List<CustomNotificationRecipientExt>()
            {
                new CustomNotificationRecipientExt()
                {
                    RecipientToOverride = recipient,
                    Recipients = new List<NotificationRecipientExt>()
                    {
                        new NotificationRecipientExt()
                        {
                            EmailAddress = "andreas@hammerbeck.no",
                        }
                    }
                }
            };
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipient])
                .WithNotificationTemplate(NotificationTemplateExt.GenericAltinnMessage)
                .WithNotificationChannel(NotificationChannelExt.Email)
                .WithCustomNotificationRecipients(customRecipients)
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
            Assert.Equal(InitializedNotificationStatusExt.Success, content?.Correspondences?.First()?.Notifications?.First().Status);
            Assert.Equal(orderId, content?.Correspondences?.First()?.Notifications?.First().OrderId);
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
            Assert.Equal(InitializedNotificationStatusExt.Success, content?.Correspondences?.First()?.Notifications?.First().Status);
            Assert.Equal(orderId, content?.Correspondences?.First()?.Notifications?.First().OrderId);
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
            Assert.Equal(InitializedNotificationStatusExt.Success, content?.Correspondences?.First()?.Notifications?.First().Status);
            Assert.Equal(orderId, content?.Correspondences?.First()?.Notifications?.First().OrderId);
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
            Assert.Equal(InitializedNotificationStatusExt.MissingContact, content?.Correspondences?.First()?.Notifications?.First().Status);
            Assert.Equal(orderId, content?.Correspondences?.First()?.Notifications?.First().OrderId);
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
            Assert.Equal(InitializedNotificationStatusExt.Failure, content?.Correspondences?.First()?.Notifications?.First().Status);
            Assert.Equal(Guid.Empty, content?.Correspondences?.First()?.Notifications?.First().OrderId);
        }
    }
}
