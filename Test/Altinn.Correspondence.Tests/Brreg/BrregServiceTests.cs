using Altinn.Correspondence.Core.Models.Brreg;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Integrations.Brreg;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;
using Altinn.Correspondence.Core.Exceptions;

namespace Altinn.Correspondence.Tests.Brreg
{
    public class BrregServiceTests
    {
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<IOptions<GeneralSettings>> _mockOptions;
        private readonly Mock<ILogger<BrregService>> _mockLogger;
        private readonly BrregService _service;

        public BrregServiceTests()
        {
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object)
            {
                BaseAddress = new Uri("https://test.brreg.no/api/")
            };
            
            _mockOptions = new Mock<IOptions<GeneralSettings>>();
            _mockOptions.Setup(x => x.Value).Returns(new GeneralSettings
            {
                BrregBaseUrl = "https://test.brreg.no/api/"
            });
            
            _mockLogger = new Mock<ILogger<BrregService>>();
            
            _service = new BrregService(_httpClient, _mockOptions.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task GetOrganizationRolesAsync_WhenSuccessfulResponse_ReturnsRoles()
        {
            // Arrange
            var organizationNumber = "123456789";
            var expectedResponse = new OrganizationRoles
            {
                RoleGroups = new List<RoleGroup>
                {
                    new RoleGroup
                    {
                        Type = new TypeInfo { Code = "STYR", Description = "Styre" },
                        Roles = new List<Role>
                        {
                            new Role
                            {
                                Type = new TypeInfo { Code = "LEDE", Description = "Daglig leder" },
                                HasResigned = false
                            }
                        }
                    }
                }
            };

            var jsonResponse = JsonSerializer.Serialize(expectedResponse);

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Get &&
                        req.RequestUri!.ToString().EndsWith($"enheter/{organizationNumber}/roller")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(jsonResponse)
                });

            // Act
            var result = await _service.GetOrganizationRolesAsync(organizationNumber);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.RoleGroups);
            Assert.Single(result.RoleGroups);
            Assert.Equal("STYR", result.RoleGroups[0].Type?.Code);
            Assert.Single(result.RoleGroups[0].Roles!);
            Assert.Equal("LEDE", result.RoleGroups[0].Roles![0].Type?.Code);
        }

        [Fact]
        public async Task GetOrganizationRolesAsync_WhenNotFound_ThrowsBrregNotFoundException()
        {
            // Arrange
            var organizationNumber = "123456789";

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Get &&
                        req.RequestUri!.ToString().EndsWith($"enheter/{organizationNumber}/roller")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.NotFound,
                    Content = new StringContent("Not found")
                });

            // Act & Assert
            var exception = await Assert.ThrowsAsync<BrregNotFoundException>(
                () => _service.GetOrganizationRolesAsync(organizationNumber));
            
            Assert.Equal(organizationNumber, exception.OrganizationNumber);
            Assert.Contains(organizationNumber, exception.Message);
        }

        [Fact]
        public async Task GetOrganizationRolesAsync_WhenOtherError_ThrowsHttpRequestException()
        {
            // Arrange
            var organizationNumber = "123456789";

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Get &&
                        req.RequestUri!.ToString().EndsWith($"enheter/{organizationNumber}/roller")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    Content = new StringContent("Server error")
                });

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => _service.GetOrganizationRolesAsync(organizationNumber));
        }

        [Fact]
        public async Task GetOrganizationDetailsAsync_WhenSuccessfulResponse_ReturnsDetails()
        {
            // Arrange
            var organizationNumber = "123456789";
            var expectedResponse = new OrganizationDetails
            {
                OrganizationNumber = organizationNumber,
                Name = "Test Organization",
                IsBankrupt = false,
                DeletionDate = null
            };

            var jsonResponse = JsonSerializer.Serialize(expectedResponse);

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Get &&
                        req.RequestUri!.ToString().EndsWith($"enheter/{organizationNumber}")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(jsonResponse)
                });

            // Act
            var result = await _service.GetOrganizationDetailsAsync(organizationNumber);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(organizationNumber, result.OrganizationNumber);
            Assert.Equal("Test Organization", result.Name);
            Assert.False(result.IsBankrupt);
            Assert.False(result.IsDeleted);
        }

        [Fact]
        public async Task GetOrganizationDetailsAsync_WhenNotFound_ThrowsBrregNotFoundException()
        {
            // Arrange
            var organizationNumber = "123456789";

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Get &&
                        req.RequestUri!.ToString().EndsWith($"enheter/{organizationNumber}")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.NotFound,
                    Content = new StringContent("Not found")
                });

            // Act & Assert
            var exception = await Assert.ThrowsAsync<BrregNotFoundException>(
                () => _service.GetOrganizationDetailsAsync(organizationNumber));
            
            Assert.Equal(organizationNumber, exception.OrganizationNumber);
            Assert.Contains(organizationNumber, exception.Message);
        }

        [Fact]
        public async Task GetOrganizationDetailsAsync_WhenOtherError_ThrowsHttpRequestException()
        {
            // Arrange
            var organizationNumber = "123456789";

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Get &&
                        req.RequestUri!.ToString().EndsWith($"enheter/{organizationNumber}")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    Content = new StringContent("Server error")
                });

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => _service.GetOrganizationDetailsAsync(organizationNumber));
        }
    }
}