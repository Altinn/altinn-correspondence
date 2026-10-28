namespace Altinn.Correspondence.Core.Models.Enums;

public enum ForwardingConstraints
{
    /// <summary>
    /// No forwarding constraints apply. This also returns when forwarding is not allowed, as the constraints are only relevant when forwarding is allowed.
    /// </summary>
    None = 0,

    /// <summary>
    /// Forwarding is allowed, but the attachments exceed max limit
    /// </summary>
    AttachmentsExceedMaxLimit = 1,

    /// <summary>
    /// Forwarding is allowed, but the user does not have access to all attachments, so they cannot be included.
    /// Takes precedence over ExpiringAttachments and AttachmentsExceedMaxLimit.
    /// </summary>
    NotAccessToAllAttachments = 2,

    /// <summary>
    /// Forwarding is allowed, but one or more attachments expire, so they cannot be included.
    /// Takes precedence over AttachmentsExceedMaxLimit.
    /// </summary>
    ExpiringAttachments = 3,
}