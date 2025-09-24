using System.Globalization;
using System.Text;
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

    /// <summary>
    /// Sanitizes a string for safe logging by escaping control characters and HTML/XML entities
    /// to prevent log injection and HTML injection attacks.
    /// </summary>
    /// <param name="input">The input string to sanitize.</param>
    /// <returns>A sanitized string safe for logging.</returns>
    public static string SanitizeForLogging(this string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        
        // Replace control characters that could be used for log injection
        var sanitized = input
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t")
            .Replace("\0", "\\0")
            .Replace("\b", "\\b")
            .Replace("\f", "\\f")
            .Replace("\v", "\\v");
        
        // Replace HTML/XML characters to prevent HTML injection in log viewers
        sanitized = sanitized
            .Replace("&", "&amp;")  // Must be first to avoid double-encoding
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#x27;");
        
        // Remove or replace other potentially dangerous control characters
        var result = new StringBuilder();
        foreach (char c in sanitized)
        {
            if (char.IsControl(c) && c != '\u0009' && c != '\u000A' && c != '\u000D') // Allow tab, LF, CR (already escaped above)
            {
                result.Append($"\\u{(int)c:X4}");
            }
            else
            {
                result.Append(c);
            }
        }
        
        // Limit length to prevent log flooding
        var finalResult = result.ToString();
        if (finalResult.Length > 1000)
        {
            finalResult = finalResult.Substring(0, 997) + "...";
        }
        
        return finalResult;
    }

    public static bool IsWithISO6523Prefix(this string identifier)
    {
        return identifier.StartsWith("0192:");
    }

    public static bool IsWithPartyUuidPrefix(this string identifier)
    {
        return identifier.StartsWith(UrnConstants.PartyUuid);
    }

    /// <summary>
    /// Adds a prefix to the identifier if it is a organization number (9 digits) or social security number (11 digits).
    /// </summary>
    /// <param name="identifier">The organization number or social security number to add the prefix to.</param>
    /// <returns>The organization number or social security number with the prefix.</returns>
    /// <exception cref="ArgumentException">Thrown if the identifier is not a valid organization number or social security number.</exception>
    public static string WithUrnPrefix(this string identifier)
    {
        if (identifier.StartsWith(UrnConstants.OrganizationNumberAttribute)
                || identifier.StartsWith(UrnConstants.PersonIdAttribute))
        {
            return identifier;
        }
        if (identifier.IsOrganizationNumber())
        {
            return $"{UrnConstants.OrganizationNumberAttribute}:{identifier}";
        }
        else if (identifier.IsSocialSecurityNumberWithNoPrefix())
        {
            return $"{UrnConstants.PersonIdAttribute}:{identifier}";
        }
        throw new ArgumentException("Identifier is not a valid organization number or social security number", nameof(identifier));
    }
}