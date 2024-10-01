namespace Altinn.Correspondence.Core.Models.Enums;
/// <summary>
/// Defines how to filter the correspondences returned by the GetCorrespondences endpoint
/// </summary>
public enum CorrespondencesRoleType
{
    /// <summary>
    /// Only return the correspondences where the consumer is the recipient of the correspondence
    /// </summary>
    Recipient,

    /// <summary>
    /// Only return the correspondences where the consumer is the sender of the correspondence
    /// </summary>
    Sender,

    /// <summary>
    /// Only return the correspondences where the consumer is the recipient or sender of the correspondence
    /// </summary>
    RecipientAndSender,
}