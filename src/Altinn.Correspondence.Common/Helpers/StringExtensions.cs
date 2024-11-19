using System.Text.RegularExpressions;
namespace Altinn.Correspondence.Common.Helpers;
public static class StringExtensions
{
    public static bool IsSocialSecurityNumber(this string identifier)
    {
        Regex ssnPattern = new(@"^\d{11}$");
        return ssnPattern.IsMatch(identifier);
    }

    public static bool IsOrganizationNumber(this string identifier)
    {
        Regex orgPattern = new(@"^(?:\d{9}|\d{4}:\d{9})$");
        return orgPattern.IsMatch(identifier);
    }

    public static string GetOrgNumberWithoutPrefix(this string orgNumber)
    {
        var parts = orgNumber.Split(':');
        return parts[^1];
    }
}