﻿using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Application.GetCorrespondences;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Legacy.Base;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using System.Net;
using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Core.Services;
using Moq;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Tests.Fixtures;

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
            await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/overview"); // Fetch in order to be able to Confirm
            await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/confirm", null); // Update to Confirmed in order to be able to Archive
            listPayload.Status = CorrespondenceStatusExt.Confirmed;
            correspondenceList = await _legacyClient.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", listPayload);
            response = await correspondenceList.Content.ReadFromJsonAsync<LegacyGetCorrespondencesResponse>(_serializerOptions);
            Assert.True(response?.Items.Count > 0);
            await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/archive", null);
            listPayload.Status = CorrespondenceStatusExt.Archived;
            correspondenceList = await _legacyClient.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", listPayload);
            response = await correspondenceList.Content.ReadFromJsonAsync<LegacyGetCorrespondencesResponse>(_serializerOptions);
            Assert.True(response?.Items.Count > 0);
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
            Assert.True(response?.Items.Count > 0);
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
