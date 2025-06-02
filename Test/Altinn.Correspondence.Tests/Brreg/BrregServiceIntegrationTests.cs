using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Brreg;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

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
        private readonly IBrregService _service;

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
            
            _service = new BrregService(_httpClient, _options, _logger);
        }

        [Fact]
        public async Task GetOrganizationRolesAsync_RealApiCall_ReturnsRoles()
        {
            // Arrange
            var organizationNumber = "991825827"; // DigDir

            var requiredRoles = new List<string> { "BEST", "DAGL", "DTPR", "DTSO", "INNH", "LEDE"}; // Required roles for confidential correspondence

            // Act
            var result = await _service.GetOrganizationRolesAsync(organizationNumber);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.RoleGroups);
            Assert.True(result.RoleGroups.Count > 0, "Should have at least one role group");
            Assert.True(result.HasAnyOfRoles(requiredRoles), "Should have at least one of the required roles");
        }

        [Fact]
        public async Task GetOrganizationDetailsAsync_RealApiCall_ReturnsDetails()
        {
            // Arrange
            var organizationNumber = "991825827"; // DigDir

            // Act
            var result = await _service.GetOrganizationDetailsAsync(organizationNumber);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(organizationNumber, result.OrganizationNumber);
            Assert.NotNull(result.Name);
            Assert.False(result.IsDeleted);
            Assert.False(result.IsBankrupt);
            Assert.NotNull(result.OrganizationNumber);
        }

        [Fact]
        public async Task GetOrganizationDetailsAsync_RealApiCall_WithInvalidOrg_ThrowsException()
        {
            // Arrange
            var organizationNumber = "000000000"; // Invalid

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => 
                _service.GetOrganizationDetailsAsync(organizationNumber));
        }
    }
} 