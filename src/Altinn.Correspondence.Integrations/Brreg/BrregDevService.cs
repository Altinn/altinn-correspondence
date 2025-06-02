using Altinn.Correspondence.Core.Models.Brreg;
using Altinn.Correspondence.Core.Services;

namespace Altinn.Correspondence.Integrations.Brreg
{
    /// <summary>
    /// Development implementation of IBrregService for local testing
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="BrregDevService"/> class.
    /// </remarks>
    public class BrregDevService() : IBrregService
    {
        public Task<OrganizationDetails> GetOrganizationDetailsAsync(string organizationNumber, CancellationToken cancellationToken = default)
        {
            // Returns mock data for development testing
            var details = new OrganizationDetails
            {
                OrganizationNumber = organizationNumber,
                Name = "Test Organization",
                IsBankrupt = false,
                DeletionDate = null
            };
            
            return Task.FromResult(details);
        }

        public Task<OrganizationRoles> GetOrganizationRolesAsync(string organizationNumber, CancellationToken cancellationToken = default)
        {
            // Returns mock data for development testing
            var roles = new OrganizationRoles();
            
            var roleGroup = new RoleGroup
            {
                Type = new TypeInfo
                {
                    Code = "STYR",
                    Description = "Styre"
                }
            };
            
            roleGroup.Roles = new List<Role>
            {
                new Role
                {
                    Type = new TypeInfo
                    {
                        Code = "LEDE",
                        Description = "Daglig leder"
                    },
                    HasResigned = false
                },
                new Role
                {
                    Type = new TypeInfo
                    {
                        Code = "NEST",
                        Description = "Nestleder"
                    },
                    HasResigned = false
                }
            };
            
            roles.RoleGroups = new List<RoleGroup> { roleGroup };
            
            return Task.FromResult(roles);
        }
    }
} 