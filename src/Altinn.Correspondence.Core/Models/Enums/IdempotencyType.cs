namespace Altinn.Correspondence.Core.Models.Enums;

/// <summary>
/// Represents different types of idempotency keys that can be used for a correspondence.
/// This enum is used to support creating idempotent requests at the correspondence level.
/// </summary>
public enum IdempotencyType
{
    /// <summary>
    /// Indicates that this is a key for activity on Dialogporten
    /// </summary>
    DialogportenActivity = 0,

    /// <summary>
    /// Indicates that this is a idempotency key for a correspondence
    /// </summary>
    Correspondence = 1
} 