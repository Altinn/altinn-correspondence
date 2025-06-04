using System.Text.Json.Serialization;

namespace Altinn.Correspondence.Core.Models.Brreg
{
    /// <summary>
    /// Model for organization roles from Brønnøysundregistrene
    /// </summary>
    public class OrganizationRoles
    {
        /// <summary>
        /// List of role groups in the organization
        /// </summary>
        [JsonPropertyName("rollegrupper")]
        public List<RoleGroup>? RoleGroups { get; set; }

        /// <summary>
        /// Checks if the organization has any of the specified roles registered on a person that is not deceased
        /// </summary>
        /// <param name="roleCodes">Role codes to check for</param>
        /// <returns>True if any of the roles are found, false otherwise</returns>
        public bool HasAnyOfRolesOnPerson(IEnumerable<string> roleCodes)
        {
            if (RoleGroups == null)
                return false;
                
            foreach (var group in RoleGroups)
            {
                if (group.Roles == null) 
                    continue;
                    
                foreach (var role in group.Roles)
                {
                    if (role.HasResigned)
                        continue;

                    if (role.Person != null && !role.Person.IsDead)
                    {
                        if (role.Type?.Code != null && roleCodes.Contains(role.Type.Code))
                            return true;
                    }
                }
            }
            
            return false;
        }
    }

    /// <summary>
    /// Group of roles in an organization (e.g., board, management)
    /// </summary>
    public class RoleGroup
    {
        /// <summary>
        /// Type information of the role group
        /// </summary>
        [JsonPropertyName("type")]
        public TypeInfo? Type { get; set; }

        /// <summary>
        /// Roles within this group
        /// </summary>
        [JsonPropertyName("roller")]
        public List<Role>? Roles { get; set; }
    }

    /// <summary>
    /// Role in an organization
    /// </summary>
    public class Role
    {
        /// <summary>
        /// Gets or sets the type of role
        /// </summary>
        [JsonPropertyName("type")]
        public TypeInfo? Type { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the person has resigned from the role
        /// </summary>
        [JsonPropertyName("fratraadt")]
        public bool HasResigned { get; set; }

        /// <summary>
        /// Gets or sets the person information (if the role is a person)
        /// </summary>
        [JsonPropertyName("person")]
        public Person? Person { get; set; }

        /// <summary>
        /// Gets or sets the organization information (if the role is an organization)
        /// </summary>
        [JsonPropertyName("enhet")]
        public Organization? Organization { get; set; }
    }

    /// <summary>
    /// Type information with code and description
    /// </summary>
    public class TypeInfo
    {
        /// <summary>
        /// Gets or sets the code of the type
        /// </summary>
        [JsonPropertyName("kode")]
        public string? Code { get; set; }

        /// <summary>
        /// Gets or sets the description of the type
        /// </summary>
        [JsonPropertyName("beskrivelse")]
        public string? Description { get; set; }
    }

    public class Person
    {
        [JsonPropertyName("erDoed")]
        public bool IsDead { get; set; }
    }

    public class Organization
    {
        [JsonPropertyName("organisasjonsnummer")]
        public string? OrganizationNumber { get; set; }
    }
} 