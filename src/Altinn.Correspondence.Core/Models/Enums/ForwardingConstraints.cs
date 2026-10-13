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
}