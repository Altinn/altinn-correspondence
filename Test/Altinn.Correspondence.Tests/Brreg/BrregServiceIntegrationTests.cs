using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Integrations.Brreg;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Altinn.Correspondence.Common.Caching;
using Microsoft.Extensions.Caching.Hybrid;

namespace Altinn.Correspondence.Tests.Brreg
{
    /// <summary>
    /// Tests for BrregService using the actual Brreg API.
    /// These tests use real network calls.
    /// </summary>
    public class BrregRealIntegrationTests
    {
        private readonly HttpClient _httpClient;
        private readonly IOptions<GeneralSettings> _options;
        private readonly ILogger<BrregService> _logger;
        private readonly Mock<IHybridCacheWrapper> _mockCache;
        private readonly BrregService _service;

        public BrregRealIntegrationTests()
        {            
            _httpClient = new HttpClient();
            
            var settings = new GeneralSettings
            {
                BrregBaseUrl = "https://data.brreg.no/enhetsregisteret/api/"
            };
            
            _options = Options.Create(settings);
            
            var loggerMock = new Mock<ILogger<BrregService>>();
            _logger = loggerMock.Object;

            _mockCache = new Mock<IHybridCacheWrapper>();
            _mockCache.Setup(x => x.GetOrCreateAsync<byte[]>(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, ValueTask<byte[]>>>(),
                It.IsAny<HybridCacheEntryOptions>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((byte[])null!));
            
            _service = new BrregService(_httpClient, _options, _logger, _mockCache.Object);
        }

        [Fact]
        public async Task GetOrganizationRolesAsync_RealApiCall_ReturnsRoles()
        {
            // Arrange
            var organizationNumber = "991825827"; // DigDir

            // Act
            var method = typeof(BrregService).GetMethod("GetOrganizationRolesAsync", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var resultTask = method!.Invoke(_service, new object[] { organizationNumber, default(CancellationToken) }) as Task<Altinn.Correspondence.Integrations.Brreg.Models.OrganizationRolesResponse>;
            var result = await resultTask!;

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.RoleGroups);
            Assert.True(result.RoleGroups.Count > 0, "Should have at least one role group");
        }

        [Fact]
        public async Task CheckOrganizationRolesAsync_RealApiCall_ValidatesRoles()
        {
            // Arrange
            var organizationNumber = "991825827"; // DigDir
            var roles = new[] { "BEST", "DAGL", "DTPR", "DTSO", "INNH", "LEDE"};

            // Act
            var result = await _service.HasAnyOfOrganizationRolesAsync(organizationNumber, roles);

            // Assert
            Assert.True(result, "API call should succeed assuming the roles are registered");
        
        }

        [Fact]
        public async Task CheckOrganizationRolesAsync_RealApiCall_WithInvalidOrg_ThrowsException()
        {
            // Arrange
            var organizationNumber = "000000000"; // Invalid
            var roles = new[] { "BEST", "DAGL", "DTPR", "DTSO", "INNH", "LEDE"};

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => 
                _service.HasAnyOfOrganizationRolesAsync(organizationNumber, roles));
        }

        [Fact]
        public async Task GetOrganizationDetailsAsync_RealApiCall_ReturnsDetails()
        {
            // Arrange
            var organizationNumber = "991825827"; // DigDir

            // Act
            var method = typeof(BrregService).GetMethod("GetOrganizationDetailsAsync", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var resultTask = method!.Invoke(_service, new object[] { organizationNumber, default(CancellationToken) }) as Task<Altinn.Correspondence.Integrations.Brreg.Models.OrganizationDetailsResponse>;
            var result = await resultTask!;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(organizationNumber, result.OrganizationNumber);
            Assert.NotNull(result.Name);
        }

        [Fact]
        public async Task IsOrganizationBankrupt_RealApiCall_WithActiveOrg_ReturnsFalse()
        {
            // Arrange
            var organizationNumber = "991825827"; // DigDir

            // Act
            var result = await _service.IsOrganizationBankrupt(organizationNumber);

            // Assert
            Assert.False(result, "Active organization should not be bankrupt");
        }

        [Fact]
        public async Task IsOrganizationDeleted_RealApiCall_WithActiveOrg_ReturnsFalse()
        {
            // Arrange
            var organizationNumber = "991825827"; // DigDir

            // Act
            var result = await _service.IsOrganizationDeleted(organizationNumber);

            // Assert
            Assert.False(result, "Active organization should not be deleted");
        }

        [Fact]
        public async Task GetOrganizationDetailsAsync_RealApiCall_WithInvalidOrg_ThrowsException()
        {
            // Arrange
            var organizationNumber = "000000000"; // Invalid

            // Act & Assert
            var method = typeof(BrregService).GetMethod("GetOrganizationDetailsAsync", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            await Assert.ThrowsAsync<HttpRequestException>(async () => { 
                var resultTask = method!.Invoke(_service, new object[] { organizationNumber, default(CancellationToken) }) as Task<Altinn.Correspondence.Integrations.Brreg.Models.OrganizationDetailsResponse>;
                await resultTask!;
            });
        }

        [Fact]
        public async Task IsOrganizationBankrupt_RealApiCall_WithInvalidOrg_ThrowsException()
        {
            // Arrange
            var organizationNumber = "000000000"; // Invalid

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => 
                _service.IsOrganizationBankrupt(organizationNumber));
        }

        [Fact]
        public async Task IsOrganizationDeleted_RealApiCall_WithInvalidOrg_ThrowsException()
        {
            // Arrange
            var organizationNumber = "000000000"; // Invalid

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => 
                _service.IsOrganizationDeleted(organizationNumber));
        }
    }
} 