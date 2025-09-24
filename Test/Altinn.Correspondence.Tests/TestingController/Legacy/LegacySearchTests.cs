using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.GetCorrespondences;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Legacy.Base;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http.Json;

namespace Altinn.Correspondence.Tests.TestingController.Legacy
{
    [Collection(nameof(CustomWebApplicationTestsCollection))]
    public class LegacySearchTests : LegacyTestBase
    {
        public LegacySearchTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }
        [Fact]
        public async Task GetCorrespondences()
        {
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _serializerOptions, correspondence.CorrespondenceId, CorrespondenceStatusExt.Published);

            var correspondenceList = await _legacyClient.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", GetBasicLegacyGetCorrespondenceRequestExt());
            var response = await correspondenceList.Content.ReadFromJsonAsync<LegacyGetCorrespondencesResponse>(_serializerOptions);

            Assert.True(response?.Items.Count > 0);
            Assert.Contains(response?.Items, c => c.CorrespondenceId == correspondence.CorrespondenceId);
        }

        [Fact]
        public async Task GetCorrespondencesFromTokenOnly()
        {
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());

            var listPayload = GetBasicLegacyGetCorrespondenceRequestExt();
            listPayload.InstanceOwnerPartyIdList = new int[] { };
            var correspondenceList = await _legacyClient.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", listPayload);
            var response = await correspondenceList.Content.ReadFromJsonAsync<LegacyGetCorrespondencesResponse>(_serializerOptions);
            Assert.Equal(HttpStatusCode.OK, correspondenceList.StatusCode);
        }
        [Fact]
        public async Task GetCorrespondences_With_Different_statuses()
        {
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _serializerOptions, correspondence.CorrespondenceId, CorrespondenceStatusExt.Published);
            var listPayload = GetBasicLegacyGetCorrespondenceRequestExt();
            listPayload.Status = CorrespondenceStatusExt.Published;

            var correspondenceList = await _legacyClient.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", listPayload);
            var response = await correspondenceList.Content.ReadFromJsonAsync<LegacyGetCorrespondencesResponse>(_serializerOptions);
            Assert.True(response?.Items.Count > 0);
            Assert.Contains(response?.Items, c => c.CorrespondenceId == correspondence.CorrespondenceId);

            await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/overview"); // Fetch in order to be able to Confirm
            await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/confirm", null); // Update to Confirmed in order to be able to Archive
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _serializerOptions, correspondence.CorrespondenceId, CorrespondenceStatusExt.Confirmed);

            listPayload.Status = CorrespondenceStatusExt.Confirmed;
            correspondenceList = await _legacyClient.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", listPayload);
            response = await correspondenceList.Content.ReadFromJsonAsync<LegacyGetCorrespondencesResponse>(_serializerOptions);
            Assert.True(response?.Items.Count > 0);
            Assert.Contains(response?.Items, c => c.CorrespondenceId == correspondence.CorrespondenceId);
            await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/archive", null);
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _serializerOptions, correspondence.CorrespondenceId, CorrespondenceStatusExt.Archived);

            listPayload.Status = CorrespondenceStatusExt.Archived;
            correspondenceList = await _legacyClient.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", listPayload);
            response = await correspondenceList.Content.ReadFromJsonAsync<LegacyGetCorrespondencesResponse>(_serializerOptions);
            Assert.True(response?.Items.Count > 0);
            Assert.Contains(response?.Items, c => c.CorrespondenceId == correspondence.CorrespondenceId);
        }

        [Fact]
        public async Task GetCorrespondences_With_Archived()
        {
            var payload = new CorrespondenceBuilder()
                      .CreateCorrespondence()
                      .WithDueDateTime(DateTimeOffset.UtcNow.AddDays(1))
                      .WithConfirmationNeeded(true)
                      .Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _serializerOptions, correspondence.CorrespondenceId, CorrespondenceStatusExt.Published);

            //  Act
            var fetchResponse = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/overview"); // Fetch in order to be able to Confirm
            Assert.Equal(HttpStatusCode.OK, fetchResponse.StatusCode);
            var confirmResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/confirm", null); // Update to Confirmed in order to be able to Archive
            Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
            var archiveResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/archive", null);
            Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);
            var listPayload = GetBasicLegacyGetCorrespondenceRequestExt();
            listPayload.IncludeActive = false;
            listPayload.IncludeArchived = true;
            listPayload.IncludeDeleted = false;
            var correspondenceList = await _legacyClient.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", listPayload);
            var response = await correspondenceList.Content.ReadFromJsonAsync<LegacyGetCorrespondencesResponse>(_serializerOptions);
            Assert.True(response?.Items.Count > 0);
            Assert.Contains(response?.Items, c => c.CorrespondenceId == correspondence.CorrespondenceId);

        }

        [Fact]
        public async Task GetCorrespondences_With_ArchivePurged()
        {
            var payload = new CorrespondenceBuilder()
                      .CreateCorrespondence()
                      .WithDueDateTime(DateTimeOffset.UtcNow.AddDays(1))
                      .WithConfirmationNeeded(false)
                      .Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _serializerOptions, correspondence.CorrespondenceId, CorrespondenceStatusExt.Published);

            var archiveResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/archive", null);
            Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _serializerOptions, correspondence.CorrespondenceId, CorrespondenceStatusExt.Archived);

            var listPayload = GetBasicLegacyGetCorrespondenceRequestExt();
            listPayload.IncludeActive = false;
            listPayload.IncludeArchived = true;
            listPayload.IncludeDeleted = false;

            var correspondenceList = await _legacyClient.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", listPayload);
            var response = await correspondenceList.Content.ReadFromJsonAsync<LegacyGetCorrespondencesResponse>(_serializerOptions);
            Assert.Contains(response?.Items, c => c.CorrespondenceId == correspondence.CorrespondenceId); // Should be in list before purge

            var purgeResponse = await _legacyClient.DeleteAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/purge");
            Assert.Equal(HttpStatusCode.OK, purgeResponse.StatusCode);

            var correspondenceList2 = await _legacyClient.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", listPayload);
            var response2 = await correspondenceList2.Content.ReadFromJsonAsync<LegacyGetCorrespondencesResponse>(_serializerOptions);
            Assert.DoesNotContain(response2?.Items, c => c.CorrespondenceId == correspondence.CorrespondenceId); // Should not longer be in archive list after purge

            var listPayload3 = GetBasicLegacyGetCorrespondenceRequestExt();
            listPayload3.IncludeActive = false;
            listPayload3.IncludeArchived = false;
            listPayload3.IncludeDeleted = true;
            var correspondenceList3 = await _legacyClient.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", listPayload3);
            var response3 = await correspondenceList3.Content.ReadFromJsonAsync<LegacyGetCorrespondencesResponse>(_serializerOptions);
            Assert.DoesNotContain(response3?.Items, c => c.CorrespondenceId == correspondence.CorrespondenceId); // Should NOT be in deleted list after purge
        }

        [Fact]
        public async Task GetCorrespondences_With_Purged()
        {
            var payload = new CorrespondenceBuilder()
                      .CreateCorrespondence()
                      .WithDueDateTime(DateTimeOffset.UtcNow.AddDays(1))
                      .WithConfirmationNeeded(true)
                      .Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _serializerOptions, correspondence.CorrespondenceId, CorrespondenceStatusExt.Published);

            await _legacyClient.DeleteAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/purge");

            var listPayload = GetBasicLegacyGetCorrespondenceRequestExt();
            listPayload.IncludeActive = false;
            listPayload.IncludeArchived = false;
            listPayload.IncludeDeleted = true;
            var correspondenceList = await _legacyClient.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", listPayload);
            var response = await correspondenceList.Content.ReadFromJsonAsync<LegacyGetCorrespondencesResponse>(_serializerOptions);
            Assert.DoesNotContain(response?.Items, c => c.CorrespondenceId == correspondence.CorrespondenceId); // Should NOT be in deleted list after purge as only soft deletes should be there
        }

        [Fact]
        public async Task GetCorrespondences_Archived_NoPurged()
        {
            var payload = new CorrespondenceBuilder()
                      .CreateCorrespondence()
                      .WithDueDateTime(DateTimeOffset.UtcNow.AddDays(1))
                      .WithConfirmationNeeded(true)
                      .Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _serializerOptions, correspondence.CorrespondenceId, CorrespondenceStatusExt.Published);

            await _legacyClient.DeleteAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/purge");

            var listPayload = GetBasicLegacyGetCorrespondenceRequestExt();
            listPayload.IncludeActive = false;
            listPayload.IncludeArchived = true;
            listPayload.IncludeDeleted = false;
            var correspondenceList = await _legacyClient.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", listPayload);
            var response = await correspondenceList.Content.ReadFromJsonAsync<LegacyGetCorrespondencesResponse>(_serializerOptions);
            Assert.DoesNotContain(response?.Items, c => c.CorrespondenceId == correspondence.CorrespondenceId);
        }

        [Fact]
        public async Task LegacyGetCorrespondences_InvalidPartyId_ReturnsUnauthorized()
        {
            using var factory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var mockRegisterService = new Mock<IAltinnRegisterService>();
                mockRegisterService
                    .Setup(service => service.LookUpPartyByPartyId(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((Party?)null);
                services.AddSingleton(mockRegisterService.Object);
            });
            var failClient = factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.LegacyScope), (_partyIdClaim, "123"));
            var listPayload = GetBasicLegacyGetCorrespondenceRequestExt();
            var response = await failClient.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", listPayload);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task LegacyGetCorrespondences_InvalidDateTimes_GivesBadRequest()
        {
            var listPayload = GetBasicLegacyGetCorrespondenceRequestExt();
            listPayload.From = DateTimeOffset.UtcNow.AddDays(1);
            listPayload.To = DateTimeOffset.UtcNow.AddDays(5);
            var correspondenceList = await _legacyClient.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", listPayload);
            Assert.Equal(HttpStatusCode.BadRequest, correspondenceList.StatusCode);

            listPayload.From = DateTimeOffset.UtcNow;
            listPayload.To = DateTimeOffset.UtcNow.AddDays(-1);
            correspondenceList = await _legacyClient.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", listPayload);
            Assert.Equal(HttpStatusCode.BadRequest, correspondenceList.StatusCode);
        }

        [Fact]
        public async Task LegacyGetCorrespondences_DuplicateAuthorizedParties_IsIgnored()
        {
            var duplicateParty = new PartyWithSubUnits()
            {
                PartyId = 1,
                Name = "hovedenhet",
                OnlyHierarchyElementWithNoAccess = false,
                SubUnits = new List<PartyWithSubUnits>()
                            {
                                new PartyWithSubUnits()
                                {
                                    PartyId = 2,
                                    Name = "underenhet",
                                    IsDeleted = false,
                                }
                            }
            };
            using var factory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var mockRegisterService = new Mock<IAltinnRegisterService>();
                mockRegisterService
                    .Setup(service => service.LookUpPartyByPartyId(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new Party()
                    {
                        OrgNumber = "123456789"
                    });
                var mockAccessManagementService = new Mock<IAltinnAccessManagementService>();
                mockAccessManagementService
                    .Setup(service => service.GetAuthorizedParties(It.IsAny<Party>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<PartyWithSubUnits>()
                    {
                        duplicateParty,
                        duplicateParty
                    });
                services.AddSingleton(mockAccessManagementService.Object);
            });
            var client = factory.CreateClientWithAddedClaims(
                ("scope", AuthorizationConstants.LegacyScope),
                (_partyIdClaim, _digdirPartyId.ToString()));
            var listPayload = GetBasicLegacyGetCorrespondenceRequestExt();
            listPayload.InstanceOwnerPartyIdList = new int[] { 1 };
            listPayload.From = DateTimeOffset.UtcNow.AddDays(-1);
            listPayload.To = DateTimeOffset.UtcNow;
            var correspondenceList = await client.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", listPayload);
            Assert.Equal(HttpStatusCode.OK, correspondenceList.StatusCode);
        }


        [Fact]
        public async Task LegacyGetCorrespondences_CalledWithUserId_UsesUserIdForAuthorizedParties()
        {
            var userId = "1234567";
            var party = new PartyWithSubUnits()
            {
                PartyId = 1,
                Name = "hovedenhet",
                OnlyHierarchyElementWithNoAccess = false,
                SubUnits = new List<PartyWithSubUnits>()
                            {
                                new PartyWithSubUnits()
                                {
                                    PartyId = 2,
                                    Name = "underenhet",
                                    IsDeleted = false,
                                }
                            }
            };
            var mockAccessManagementService = new Mock<IAltinnAccessManagementService>();
            mockAccessManagementService
                .Setup(service => service.GetAuthorizedParties(It.IsAny<Party>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<PartyWithSubUnits>()
                {
                        party
                });
            using var factory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                services.AddSingleton(mockAccessManagementService.Object);
            });
            var client = factory.CreateClientWithAddedClaims(
                ("scope", AuthorizationConstants.LegacyScope),
                (_partyIdClaim, _digdirPartyId.ToString()),
                (UrnConstants.UserId, userId));
            var listPayload = GetBasicLegacyGetCorrespondenceRequestExt();
            listPayload.InstanceOwnerPartyIdList = new int[] { 1 };
            listPayload.From = DateTimeOffset.UtcNow.AddDays(-1);
            listPayload.To = DateTimeOffset.UtcNow;
            var correspondenceList = await client.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", listPayload);
            Assert.Equal(HttpStatusCode.OK, correspondenceList.StatusCode);
            mockAccessManagementService.Verify(service => service.GetAuthorizedParties(
                It.IsAny<Party>(),
                It.Is<string>(party => party == userId),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task LegacyGetCorrespondences_NoAuthorizedParties_Fails()
        {
            using var factory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var mockRegisterService = new Mock<IAltinnRegisterService>();
                mockRegisterService
                    .Setup(service => service.LookUpPartyByPartyId(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new Party()
                    {
                        OrgNumber = "123456789"
                    });
                var mockAccessManagementService = new Mock<IAltinnAccessManagementService>();
                mockAccessManagementService
                    .Setup(service => service.GetAuthorizedParties(It.IsAny<Party>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync([]);
                services.AddSingleton(mockAccessManagementService.Object);
            });
            var client = factory.CreateClientWithAddedClaims(
                ("scope", AuthorizationConstants.LegacyScope),
                (_partyIdClaim, _digdirPartyId.ToString()));
            var listPayload = GetBasicLegacyGetCorrespondenceRequestExt();
            listPayload.InstanceOwnerPartyIdList = new int[] { 1 };
            listPayload.From = DateTimeOffset.UtcNow.AddDays(-1);
            listPayload.To = DateTimeOffset.UtcNow;
            var correspondenceList = await client.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", listPayload);
            Assert.Equal(HttpStatusCode.Unauthorized, correspondenceList.StatusCode);
        }
        [Fact]
        public async Task LegacyGetCorrespondences_ForSubUnitAndAccessToOnlySubunit_Succeeds()
        {
            using var factory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var mockRegisterService = new Mock<IAltinnRegisterService>();
                mockRegisterService
                    .Setup(service => service.LookUpPartyByPartyId(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new Party()
                    {
                        OrgNumber = "123456789"
                    });
                var mockAccessManagementService = new Mock<IAltinnAccessManagementService>();
                mockAccessManagementService
                    .Setup(service => service.GetAuthorizedParties(It.IsAny<Party>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<PartyWithSubUnits>()
                    {
                        new PartyWithSubUnits()
                        {
                            PartyId = 1,
                            Name = "hovedenhet",
                            OnlyHierarchyElementWithNoAccess = true,
                            SubUnits = new List<PartyWithSubUnits>()
                                        {
                                            new PartyWithSubUnits()
                                            {
                                                PartyId = 2,
                                                Name = "underenhet",
                                                IsDeleted = false,
                                            }
                                        }
                        }
                    });
                services.AddSingleton(mockAccessManagementService.Object);
            });
            var client = factory.CreateClientWithAddedClaims(
                ("scope", AuthorizationConstants.LegacyScope),
                (_partyIdClaim, _digdirPartyId.ToString()));
            var listPayload = GetBasicLegacyGetCorrespondenceRequestExt();
            listPayload.InstanceOwnerPartyIdList = new int[] { 2 };
            listPayload.From = DateTimeOffset.UtcNow.AddDays(-1);
            listPayload.To = DateTimeOffset.UtcNow;
            var correspondenceList = await client.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", listPayload);
            Assert.Equal(HttpStatusCode.OK, correspondenceList.StatusCode);
        }
        [Fact]
        public async Task LegacyGetCorrespondences_ForMainUnitButAccessToOnlySubunit_Fails()
        {
            using var factory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var mockRegisterService = new Mock<IAltinnRegisterService>();
                mockRegisterService
                    .Setup(service => service.LookUpPartyByPartyId(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new Party()
                    {
                        OrgNumber = "123456789"
                    });
                var mockAccessManagementService = new Mock<IAltinnAccessManagementService>();
                mockAccessManagementService
                    .Setup(service => service.GetAuthorizedParties(It.IsAny<Party>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<PartyWithSubUnits>()
                    {
                        new PartyWithSubUnits()
                        {
                            PartyId = 1,
                            Name = "hovedenhet",
                            OnlyHierarchyElementWithNoAccess = true,
                            SubUnits = new List<PartyWithSubUnits>()
                                        {
                                            new PartyWithSubUnits()
                                            {
                                                PartyId = 2,
                                                Name = "underenhet",
                                                IsDeleted = false,
                                            }
                                        }
                        }
                    });
                services.AddSingleton(mockAccessManagementService.Object);
            });
            var client = factory.CreateClientWithAddedClaims(
                ("scope", AuthorizationConstants.LegacyScope),
                (_partyIdClaim, _digdirPartyId.ToString()));
            var listPayload = GetBasicLegacyGetCorrespondenceRequestExt();
            listPayload.InstanceOwnerPartyIdList = new int[] { 1 };
            listPayload.From = DateTimeOffset.UtcNow.AddDays(-1);
            listPayload.To = DateTimeOffset.UtcNow;
            var correspondenceList = await client.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", listPayload);
            Assert.Equal(HttpStatusCode.Unauthorized, correspondenceList.StatusCode);
        }


        private LegacyGetCorrespondencesRequestExt GetBasicLegacyGetCorrespondenceRequestExt()
        {
            return new LegacyGetCorrespondencesRequestExt
            {
                InstanceOwnerPartyIdList = new int[] { },
                IncludeActive = true,
                IncludeArchived = true,
                IncludeDeleted = true,
                From = DateTimeOffset.UtcNow.AddDays(-5),
                To = DateTimeOffset.UtcNow.AddDays(5),
            };
        }

    }
}
