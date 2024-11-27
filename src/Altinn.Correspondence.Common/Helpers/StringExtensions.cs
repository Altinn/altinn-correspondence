using System.Text.RegularExpressions;
namespace Altinn.Correspondence.Common.Helpers;
public static class StringExtensions
{
    private static readonly Regex SsnPattern = new(@"^\d{11}$");
    private static readonly Regex OrgPattern = new(@"^(?:\d{9}|\d{4}:\d{9})$");

    /// <summary>
    /// Checks if the provided string is a valid social security number format.
    /// </summary>
    /// <param name="identifier">The string to validate.</param>
    /// <returns>True if the string matches a 11-digit format.</returns>
    public static bool IsSocialSecurityNumber(this string identifier)
    {
        return (!string.IsNullOrEmpty(identifier) && SsnPattern.IsMatch(identifier));
    }

    /// <summary>
    /// Checks if the provided string is a valid organization number format.
    /// </summary>
    /// <param name="identifier">The string to validate.</param>
    /// <returns>True if the string matches either a 9-digit format or a '4digits:9digits' format, false otherwise.</returns>
    public static bool IsOrganizationNumber(this string identifier)
    {
        return (!string.IsNullOrEmpty(identifier) && OrgPattern.IsMatch(identifier));
    }
    /// <summary>
    /// Extracts the identifier from a colon-separated string that may contain a prefix.
    /// </summary>
    /// <param name="orgNumber">The organization number to format</param>
    /// <returns>Returns the last sequence succeeding a colon.</returns>
    public static string WithoutPrefix(this string orgOrSsnNumber)
    {
        return orgOrSsnNumber.Split(":").Last();
    }
}