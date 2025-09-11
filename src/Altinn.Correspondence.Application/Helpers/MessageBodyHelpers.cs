using ReverseMarkdown;
using System.Text;
using System.Text.RegularExpressions;

namespace Altinn.Correspondence.Application.Helpers;

public static class MessageBodyHelpers
{
    // Altinn 2 inbox rendered both html and markdown, hence we must do same
    public static string ConvertMixedToMarkdown(string input)
    {
        var html = TextValidation.ConvertToHtml(input); // Normalizes to html
        var config = new Config
        {
            UnknownTags = Config.UnknownTagsOption.Drop, 
            GithubFlavored = true, 
            RemoveComments = true,
            SmartHrefHandling = true,
            DefaultCodeBlockLanguage = "", 
            WhitelistUriSchemes = ["http", "https", "mailto"],
        };

        var converter = new Converter(config);

        // Split by lines to handle mixed content properly
        string preprocessed = ConvertLinksToMarkdown(html);
        var processed = converter.Convert(preprocessed);
        return processed;
    }

    private static string ConvertLinksToMarkdown(string input)
    {
        string result = input;

        // Handle complex anchor tags with multiple attributes
        result = Regex.Replace(result,
            @"<a(?:\s+[^>]*?)?\s+href=[""']([^""']*)[""'](?:\s+[^>]*?)?>([^<]*?)</a>",
            "[$2]($1)",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

        // Handle simpler cases
        result = Regex.Replace(result,
            @"<a\s+href=[""']([^""']*)[""']\s*>([^<]*?)</a>",
            "[$2]($1)",
            RegexOptions.IgnoreCase);

        return result;
    }
}
