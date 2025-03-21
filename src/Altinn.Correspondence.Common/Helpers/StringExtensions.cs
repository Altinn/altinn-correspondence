using System.Globalization;
using System.Text.RegularExpressions;
namespace Altinn.Correspondence.Common.Helpers;
public static class StringExtensions
{
    private static readonly Regex SsnPattern = new(@"^\d{11}$");
    private static readonly Regex OrgPattern = new(@"^(?:\d{9}|0192:\d{9})$");
    private static readonly int[] SocialSecurityNumberWeights1 = [3, 7, 6, 1, 8, 9, 4, 5, 2, 1];
    private static readonly int[] SocialSecurityNumberWeights2 = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2, 1];

    /// <summary>
    /// Checks if the provided string is a valid social security number format.
    /// </summary>
    /// <param name="identifier">The string to validate.</param>
    /// <returns>True if the social security number of the identifier matches a 11-digit format and passes mod11 validation.</returns>
    public static bool IsSocialSecurityNumber(this string identifier)
    {
        return IsSocialSecurityNumberWithNoPrefix(identifier.WithoutPrefix());
    }

    /// <summary>
    /// Checks if the provided string is a valid social security number.
    /// </summary>
    /// <param name="identifier">The string to validate.</param>
    /// <returns>True if the string matches a 11-digit format and passes mod11 validation.</returns>
    public static bool IsSocialSecurityNumberWithNoPrefix(this string identifier)
    {
        return !string.IsNullOrWhiteSpace(identifier)
            && SsnPattern.IsMatch(identifier)
            && Mod11.TryCalculateControlDigit(identifier.AsSpan()[..9], SocialSecurityNumberWeights1, out var control1)
            && Mod11.TryCalculateControlDigit(identifier.AsSpan()[..10], SocialSecurityNumberWeights2, out var control2)
            && control1 == int.Parse(identifier[9..10], CultureInfo.InvariantCulture)
            && control2 == int.Parse(identifier[10..11], CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Checks if the provided string is a valid organization number format.
    /// </summary>
    /// <param name="identifier">The string to validate.</param>
    /// <returns>True if the string matches either a 9-digit format or a '4digits:9digits' format, false otherwise.</returns>
    public static bool IsOrganizationNumber(this string identifier)
    {
        return (!string.IsNullOrWhiteSpace(identifier) && OrgPattern.IsMatch(identifier));
    }
    /// <summary>
    /// Extracts the identifier from a colon-separated string that may contain a prefix.
    /// </summary>
    /// <param name="orgOrSsnNumber">The organization number or social security number to format</param>
    /// <returns>Returns the last sequence succeeding a colon.</returns>
    public static string WithoutPrefix(this string orgOrSsnNumber)
    {
        if (string.IsNullOrWhiteSpace(orgOrSsnNumber))
        {
            return string.Empty;
        }
        return orgOrSsnNumber.Split(":").Last();
    }
}