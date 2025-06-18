using System.Globalization;
using System.Text.RegularExpressions;
using Altinn.Correspondence.Common.Constants;
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
    /// Checks if the provided string is a valid social security number and that it has no prefix.
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
        return !string.IsNullOrWhiteSpace(identifier) && OrgPattern.IsMatch(identifier);
    }
    /// <summary>
    /// Extracts the identifier from a colon-separated string that may contain a prefix.
    /// </summary>
    /// <param name="identifierWithPrefix">An identifier with a prefix to format. f.eks an organization number or social security number</param>
    /// <returns>Returns the last sequence succeeding a colon.</returns>
    public static string WithoutPrefix(this string identifierWithPrefix)
    {
        if (string.IsNullOrWhiteSpace(identifierWithPrefix))
        {
            return string.Empty;
        }
        return identifierWithPrefix.Split(":").Last();
    }

    public static string SanitizeForLogging(this string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return input
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }

    /// <summary>
    /// Adds a prefix to the identifier if it is a organization number (9 digits).
    /// </summary>
    /// <param name="identifier">The organization number to add the prefix to.</param>
    /// <returns>The organization number with the prefix.</returns>
    /// <exception cref="ArgumentException">Thrown if the identifier is not a valid organization number.</exception>
    public static string WithPrefix(this string identifier) {
        if (identifier.IsOrganizationNumber()) {
            return $"{UrnConstants.OrganizationNumberAttribute}:{identifier}";
        }
        throw new ArgumentException("Identifier is not a valid organization number", nameof(identifier));
    }
}