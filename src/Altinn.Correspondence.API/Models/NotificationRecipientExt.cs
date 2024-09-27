
namespace Altinn.Correspondence.API.Models;

/// <summary>
/// A class representing a a recipient of a notification
/// </summary>
/// <remarks>
/// External representaion to be used in the API.
/// </remarks>
public class NotificationRecipientExt
{
    /// <summary>
    /// the email address of the recipient
    /// </summary>
    public string? EmailAddress { get; set; }

    /// <summary>
    /// the mobileNumber of the recipient
    /// </summary>
    public string? MobileNumber { get; set; }

    /// <summary>
    /// the organization number of the recipient
    /// </summary>
    public string? OrganizationNumber { get; set; }

    /// <summary>
    /// The SSN of the recipient
    /// </summary>
    public string? NationalIdentityNumber { get; set; }

    /// <summary>
    /// Boolean indicating if the recipient is reserved
    /// </summary>
    public bool? IsReserved { get; set; }
}