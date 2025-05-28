using Altinn.Correspondence.Integrations.Brreg;
using Microsoft.Extensions.Logging;
using Moq;

namespace Altinn.Correspondence.Tests.Brreg
{
    public class BrregDevServiceTests
    {
        private readonly Mock<ILogger<BrregDevService>> _mockLogger;
        private readonly BrregDevService _service;

        public BrregDevServiceTests()
        {
            _mockLogger = new Mock<ILogger<BrregDevService>>();
            _service = new BrregDevService(_mockLogger.Object);
        }

        [Fact]
        public async Task CheckOrganizationRolesAsync_ReturnsTrue()
        {
            // Arrange
            var organizationNumber = "123456789";
            var roles = new[] { "LEDE", "STYR" };

            // Act
            var result = await _service.HasOrganizationRolesAsync(organizationNumber, roles);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsOrganizationBankruptOrDeletedAsync_ReturnsFalse()
        {
            // Arrange
            var organizationNumber = "123456789";

            // Act
            var result = await _service.IsOrganizationBankruptOrDeletedAsync(organizationNumber);

            // Assert
            Assert.False(result);
        }
    }
} 