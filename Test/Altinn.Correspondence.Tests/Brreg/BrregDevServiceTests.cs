using Altinn.Correspondence.Integrations.Brreg;

namespace Altinn.Correspondence.Tests.Brreg
{
    public class BrregDevServiceTests
    {
        private readonly BrregDevService _service;
        private static readonly string[] _testRoles = ["LEDE"];

        public BrregDevServiceTests()
        {
            _service = new BrregDevService();
        }

        [Fact]
        public async Task GetOrganizationDetailsAsync_ReturnsMockData()
        {
            // Arrange
            var organizationNumber = "123456789";

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
        public async Task GetOrganizationRolesAsync_ReturnsMockData()
        {
            // Arrange
            var organizationNumber = "123456789";

            // Act
            var result = await _service.GetOrganizationRolesAsync(organizationNumber);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.RoleGroups);
            Assert.Single(result.RoleGroups);
            
            var roleGroup = result.RoleGroups[0];
            Assert.True(result.HasAnyOfRoles(_testRoles));
            Assert.NotNull(roleGroup);
            Assert.NotNull(roleGroup.Type);
            Assert.Equal("STYR", roleGroup.Type!.Code);
            Assert.NotNull(roleGroup.Roles);
            Assert.Equal(2, roleGroup.Roles!.Count);
            Assert.NotNull(roleGroup.Roles[0].Type);
            Assert.Equal("LEDE", roleGroup.Roles[0].Type!.Code);
            Assert.NotNull(roleGroup.Roles[1].Type);
            Assert.Equal("NEST", roleGroup.Roles[1].Type!.Code);
        }
    }
} 