﻿using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Correspondence.Base;
using System.Net;
using System.Net.Http.Json;

namespace Altinn.Correspondence.Tests.TestingController.Correspondence
{
    [Collection(nameof(CustomWebApplicationTestsCollection))]
    public class CorrespondenceStatusTests : CorrespondenceTestBase
    {
        public CorrespondenceStatusTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        [Fact]
        public async Task UpdateCorrespondenceStatus_CorrespondenceNotExists_Return404()
        {
            var readResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/00000000-0100-0000-0000-000000000000/markasread", null);
            Assert.Equal(HttpStatusCode.NotFound, readResponse.StatusCode);

            var confirmResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/00000000-0100-0000-0000-000000000000/confirm", null);
            Assert.Equal(HttpStatusCode.NotFound, confirmResponse.StatusCode);

            var archiveResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/00000000-0100-0000-0000-000000000000/archive", null);
            Assert.Equal(HttpStatusCode.NotFound, archiveResponse.StatusCode);
        }

        [Fact]
        public async Task UpdateCorrespondenceStatus_CorrespondenceNotPublished_Return404()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(1))
                .Build();
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            var correspondenceResponse = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            Assert.NotNull(correspondenceResponse);
            var correspondenceId = correspondenceResponse?.Correspondences.FirstOrDefault()?.CorrespondenceId;

            // Act and Assert
            var readResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/{correspondenceId}/markasread", null);
            Assert.Equal(HttpStatusCode.NotFound, readResponse.StatusCode);

            // Act and Assert
            var confirmResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/{correspondenceId}/confirm", null);
            Assert.Equal(HttpStatusCode.NotFound, confirmResponse.StatusCode);
        }

        [Fact]
        public async Task UpdateCorrespondenceStatus_CorrespondencePublished_ReturnOk()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .Build();
            var initializedCorrespondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _responseSerializerOptions, payload);
            var correspondenceId = initializedCorrespondence.CorrespondenceId;
            var correspondence = await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, correspondenceId, CorrespondenceStatusExt.Published);

            // Act
            var fetchResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}"); // Fetch in order to be able to Read
            Assert.Equal(HttpStatusCode.OK, fetchResponse.StatusCode);

            var readResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/{correspondenceId}/markasread", null);
            readResponse.EnsureSuccessStatusCode();

            // Assert
            var overview = await _senderClient.GetFromJsonAsync<CorrespondenceOverviewExt>($"correspondence/api/v1/correspondence/{correspondenceId}", _responseSerializerOptions);
            Assert.Equal(CorrespondenceStatusExt.Read, overview?.Status);
        }

        [Fact]
        public async Task UpdateCorrespondenceStatus_MarkAsRead_WithoutFetched_ReturnsBadRequest()
        {
            //  Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .Build();
            var initializedCorrespondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _responseSerializerOptions, payload);
            var correspondenceId = initializedCorrespondence.CorrespondenceId;
            var correspondence = await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, correspondenceId, CorrespondenceStatusExt.Published);

            //  Act
            var confirmResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/{correspondenceId}/markasread", null);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, confirmResponse.StatusCode);
        }

        [Fact]
        public async Task UpdateCorrespondenceStatus_ToConfirmed_WithoutFetched_ReturnsBadRequest()
        {
            //  Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .Build();
            var initializedCorrespondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _responseSerializerOptions, payload);
            var correspondenceId = initializedCorrespondence.CorrespondenceId;
            var correspondence = await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, correspondenceId, CorrespondenceStatusExt.Published);

            //  Act
            var confirmResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/{correspondenceId}/confirm", null);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, confirmResponse.StatusCode);
        }

        [Fact]
        public async Task UpdateCorrespondenceStatus_ToConfirmed_WhenCorrespondenceIsFetched_GivesOk()
        {
            //  Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .Build();
            var initializedCorrespondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _responseSerializerOptions, payload);
            var correspondenceId = initializedCorrespondence.CorrespondenceId;
            var correspondence = await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, correspondenceId, CorrespondenceStatusExt.Published);

            //  Act
            var fetchResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}");
            Assert.Equal(HttpStatusCode.OK, fetchResponse.StatusCode);
            var confirmResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/{correspondenceId}/confirm", null);

            // Assert
            Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        }
    }
}
