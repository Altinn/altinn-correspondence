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
        public Task<OrganizationDetails> GetOrganizationDetails(string organizationNumber, CancellationToken cancellationToken = default)
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

        public Task<SubOrganizationDetails> GetSubOrganizationDetails(string organizationNumber, CancellationToken cancellationToken = default)
        {
            // Returns mock data for development testing
            var details = new SubOrganizationDetails
            {
                OrganizationNumber = organizationNumber,
                Name = "Test Sub Organization",
                IsBankrupt = false,
                DeletionDate = null,
                ParentOrganizationNumber = "312585065"
            };
            
            return Task.FromResult(details);
        }

        public Task<OrganizationRoles> GetOrganizationRoles(string organizationNumber, CancellationToken cancellationToken = default)
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
                    HasResigned = false,
                    Person = new Person { IsDead = false }
                },
                new Role
                {
                    Type = new TypeInfo
                    {
                        Code = "NEST",
                        Description = "Nestleder"
                    },
                    HasResigned = false,
                    Person = new Person { IsDead = false }
                }
            };
            
            roles.RoleGroups = new List<RoleGroup> { roleGroup };
            
            return Task.FromResult(roles);
        }
    }
} 