namespace Altinn.Correspondence.Core.Models.Enums;

/// <summary>
/// Represents different types of status actions that can be performed on a correspondence or its attachments
/// </summary>
public enum StatusAction
{
    /// <summary>
    /// Indicates that a correspondence has been read
    /// </summary>
    Read,

    /// <summary>
    /// Indicates that a correspondence has been confirmed
    /// </summary>
    Confirm,

    /// <summary>
    /// Indicates that an attachment download has started
    /// </summary>
    DownloadStarted,
} 