using Altinn.Correspondence.Integrations.Brreg;
using Altinn.Correspondence.Core.Models.Brreg;

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
            var result = await _service.GetOrganizationDetails(organizationNumber);

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
            var result = await _service.GetOrganizationRoles(organizationNumber);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.RoleGroups);
            Assert.Single(result.RoleGroups);
            
            var roleGroup = result.RoleGroups[0];
            Assert.True(result.HasAnyOfRolesOnPerson(_testRoles));
            Assert.NotNull(roleGroup);
            Assert.NotNull(roleGroup.Type);
            Assert.Equal("STYR", roleGroup.Type!.Code);
            Assert.NotNull(roleGroup.Roles);
            Assert.Equal(2, roleGroup.Roles!.Count);
            Assert.NotNull(roleGroup.Roles[0].Type);
            Assert.Equal("LEDE", roleGroup.Roles[0].Type!.Code);
            Assert.NotNull(roleGroup.Roles[0].Person);
            Assert.False(roleGroup.Roles[0].Person!.IsDead);
            Assert.NotNull(roleGroup.Roles[1].Type);
            Assert.Equal("NEST", roleGroup.Roles[1].Type!.Code);
            Assert.NotNull(roleGroup.Roles[1].Person);
            Assert.False(roleGroup.Roles[1].Person!.IsDead);
        }

        [Fact]
        public void HasAnyOfRolesOnPerson_WhenRoleIsOnOrganization_ReturnsFalse()
        {
            // Arrange
            var organizationRoles = new OrganizationRoles
            {
                RoleGroups = new List<RoleGroup>
                {
                    new RoleGroup
                    {
                        Roles = new List<Role>
                        {
                            new Role
                            {
                                Type = new TypeInfo { Code = "LEDE" },
                                HasResigned = false,
                                Organization = new Organization { OrganizationNumber = "123456789" },
                                Person = null
                            }
                        }
                    }
                }
            };

            // Act
            var result = organizationRoles.HasAnyOfRolesOnPerson(_testRoles);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void HasAnyOfRolesOnPerson_WhenPersonIsDeceased_ReturnsFalse()
        {
            // Arrange
            var organizationRoles = new OrganizationRoles
            {
                RoleGroups = new List<RoleGroup>
                {
                    new RoleGroup
                    {
                        Roles = new List<Role>
                        {
                            new Role
                            {
                                Type = new TypeInfo { Code = "LEDE" },
                                HasResigned = false,
                                Person = new Person { IsDead = true }
                            }
                        }
                    }
                }
            };

            // Act
            var result = organizationRoles.HasAnyOfRolesOnPerson(_testRoles);

            // Assert
            Assert.False(result);
        }
    }
} 