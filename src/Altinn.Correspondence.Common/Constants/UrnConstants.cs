namespace Altinn.Correspondence.Common.Constants;
public static class UrnConstants
{
    /// <summary>
    /// xacml string that represents authentication level
    /// </summary>
    public const string AuthenticationLevel = "urn:altinn:authlevel";
    /// <summary>
    /// xacml string that represents minimum authentication level
    /// </summary>
    public const string MinimumAuthenticationLevel = "urn:altinn:minimum-authenticationlevel";
    /// <summary>
    /// xacml string that represents organization number
    /// </summary>
    public const string OrganizationNumberAttribute = "urn:altinn:organization:identifier-no";
    /// <summary>
    /// xacml string that represents party
    /// </summary>
    public const string Party = "urn:altinn:partyid";
    /// <summary>
    /// xacml string that represents person identifier
    /// </summary>
    public const string PersonIdAttribute = "urn:altinn:person:identifier-no";
    /// <summary>
    /// xacml string that represents resource
    /// </summary>
    public const string Resource = "urn:altinn:resource";
    /// <summary>
    /// xacml string that represents resource instance
    /// </summary>
    public const string ResourceInstance = "urn:altinn:resourceinstance";
    /// <summary>
    /// xacml string that represents session id
    /// </summary>
    public const string SessionId = "urn:altinn:sessionid";
    /// <summary>
    /// Placeholder sender value used in mappers before the actual serviceOwnerOrgNumber is determined from ResourceRegistryService
    /// </summary>
    public const string PlaceholderSender = "urn:altinn:organization:identifier-no:000000000";
    /// <summary>
    /// xacml string that refers to systemuser authentication
    /// </summary>
    public const string SystemUser = "urn:altinn:systemuser";
}