namespace Altinn.Correspondence.Core.Models.Enums;

/// <summary>
/// Represents different types of status actions that can be performed on a correspondence or its attachments.
/// This enum is used to support creating idempotent requests to Dialogporten to avoid duplicate activities being created.
/// <para>
/// The numeric values correspond to the related <see cref="CorrespondenceStatus"/> values in Dialogporten.
/// For example, Fetched (3) corresponds to <see cref="CorrespondenceStatus.Fetched"/>.
/// </para>
/// <para>
/// These values align with the corresponding status values in CorrespondenceStatusExt in the API layer for consistency across the application.
/// </para>
/// </summary>
public enum StatusAction
{
    /// <summary>
    /// Indicates that a correspondence has been fetched
    /// </summary>
    Fetched = 3,

    /// <summary>
    /// Indicates that a correspondence has been confirmed
    /// </summary>
    Confirmed = 6,

    /// <summary>
    /// Indicates that an attachment download has started
    /// </summary>
    AttachmentDownloaded = 9,
} 