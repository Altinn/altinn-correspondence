using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Integrations.Brreg;
using Altinn.Correspondence.Integrations.Brreg.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;
using Altinn.Correspondence.Common.Caching;
using Microsoft.Extensions.Caching.Hybrid;

namespace Altinn.Correspondence.Tests.Brreg
{
    public class BrregServiceTests
    {
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<IOptions<GeneralSettings>> _mockOptions;
        private readonly Mock<ILogger<BrregService>> _mockLogger;
        private readonly Mock<IHybridCacheWrapper> _mockCache;
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
            _mockCache = new Mock<IHybridCacheWrapper>();
            
            // Mock cache to always return null (cache miss)
            _mockCache.Setup(x => x.GetOrCreateAsync<byte[]>(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, ValueTask<byte[]>>>(),
                It.IsAny<HybridCacheEntryOptions>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((byte[])null!));
            
            _service = new BrregService(_httpClient, _mockOptions.Object, _mockLogger.Object, _mockCache.Object);
        }

        [Fact]
        public async Task GetOrganizationRolesAsync_WhenSuccessfulResponse_ReturnsRoles()
        {
            // Arrange
            var organizationNumber = "123456789";
            var expectedResponse = new OrganizationRolesResponse
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
        public async Task GetOrganizationRolesAsync_WhenErrorResponse_ThrowsException()
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
            await Assert.ThrowsAsync<HttpRequestException>(() => _service.GetOrganizationRolesAsync(organizationNumber));
        }

        [Fact]
        public async Task CheckOrganizationRolesAsync_WhenRolesExist_ReturnsSuccess()
        {
            // Arrange
            var organizationNumber = "123456789";
            var roles = new[] { "LEDE", "STYR" };
            
            var mockResponse = new OrganizationRolesResponse
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

            var jsonResponse = JsonSerializer.Serialize(mockResponse);

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
            var result = await _service.HasAnyOfOrganizationRolesAsync(organizationNumber, roles);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task CheckOrganizationRolesAsync_WhenSomeRolesMissing_ReturnsMissingRoles()
        {
            // Arrange
            var organizationNumber = "123456789";
            var roles = new[] { "LEDE", "NEST", "STYR" };
            
            var mockResponse = new OrganizationRolesResponse
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

            var jsonResponse = JsonSerializer.Serialize(mockResponse);

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
            var result = await _service.HasAnyOfOrganizationRolesAsync(organizationNumber, roles);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task GetOrganizationDetailsAsync_WhenSuccessfulResponse_ReturnsDetails()
        {
            // Arrange
            var organizationNumber = "123456789";
            var expectedResponse = new OrganizationDetailsResponse
            {
                OrganizationNumber = organizationNumber,
                Name = "Test Organization",
                OrganizationForm = new OrganizationForm { Code = "AS", Description = "Aksjeselskap" },
                RegistrationDate = new DateTime(2020, 1, 1),
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
        public async Task GetOrganizationDetailsAsync_WhenErrorResponse_ThrowsException()
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

            // Using a private method via reflection for testing
            var method = typeof(BrregService).GetMethod("GetOrganizationDetailsAsync", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(async () => { 
                var resultTask = method!.Invoke(_service, [organizationNumber, default(CancellationToken)]) as Task<OrganizationDetailsResponse>;
                await resultTask!;
            });
        }

        [Fact]
        public async Task IsOrganizationBankrupt_WhenOrganizationBankrupt_ReturnsTrue()
        {
            // Arrange
            var organizationNumber = "123456789";
            var mockResponse = new OrganizationDetailsResponse
            {
                OrganizationNumber = organizationNumber,
                Name = "Test Organization",
                IsBankrupt = true,
                DeletionDate = null
            };

            var jsonResponse = JsonSerializer.Serialize(mockResponse);

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
            var result = await _service.IsOrganizationBankrupt(organizationNumber);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsOrganizationBankrupt_WhenOrganizationNotBankrupt_ReturnsFalse()
        {
            // Arrange
            var organizationNumber = "123456789";
            var mockResponse = new OrganizationDetailsResponse
            {
                OrganizationNumber = organizationNumber,
                Name = "Test Organization",
                IsBankrupt = false,
                DeletionDate = null
            };

            var jsonResponse = JsonSerializer.Serialize(mockResponse);

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
            var result = await _service.IsOrganizationBankrupt(organizationNumber);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsOrganizationDeleted_WhenOrganizationDeleted_ReturnsTrue()
        {
            // Arrange
            var organizationNumber = "123456789";
            var mockResponse = new OrganizationDetailsResponse
            {
                OrganizationNumber = organizationNumber,
                Name = "Test Organization",
                IsBankrupt = false,
                DeletionDate = new DateTime(2020, 1, 1)
            };

            var jsonResponse = JsonSerializer.Serialize(mockResponse);

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
            var result = await _service.IsOrganizationDeleted(organizationNumber);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsOrganizationDeleted_WhenOrganizationNotDeleted_ReturnsFalse()
        {
            // Arrange
            var organizationNumber = "123456789";
            var mockResponse = new OrganizationDetailsResponse
            {
                OrganizationNumber = organizationNumber,
                Name = "Test Organization",
                IsBankrupt = false,
                DeletionDate = null
            };

            var jsonResponse = JsonSerializer.Serialize(mockResponse);

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
            var result = await _service.IsOrganizationDeleted(organizationNumber);

            // Assert
            Assert.False(result);
        }
    }
}