using System.Text.Json.Serialization;

namespace Altinn.Correspondence.Integrations.Brreg.Models
{
    /// <summary>
    /// Response model for the Brreg API organization roles endpoint
    /// </summary>
    public class OrganizationRolesResponse
    {
        /// <summary>
        /// Gets or sets the role groups for the organization
        /// </summary>
        [JsonPropertyName("rollegrupper")]
        public List<RoleGroup>? RoleGroups { get; set; }
    }

    /// <summary>
    /// Group of roles in an organization
    /// </summary>
    public class RoleGroup
    {
        /// <summary>
        /// Gets or sets the type of role group
        /// </summary>
        [JsonPropertyName("type")]
        public TypeInfo? Type { get; set; }

        /// <summary>
        /// Gets or sets the roles in this group
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
} 