namespace Altinn.Correspondence.Integrations.Azure;

internal class RoleAssignmentRequest
{
    public required RoleAssignmentProperties Properties { get; set; }
}

internal class RoleAssignmentProperties
{
    public required string RoleDefinitionId { get; set; }
    public required string PrincipalId { get; set; }
    public required string PrincipalType { get; set; }
}
