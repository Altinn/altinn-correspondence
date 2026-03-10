using System;
using ReverseMarkdown;
using System.Text.RegularExpressions;
using Altinn.Correspondence.Common.Helpers;

namespace Altinn.Correspondence.Application.Helpers;

public static class MessageBodyHelpers
{
    // Altinn 2 inbox rendered both html and markdown, hence we must do same
    public static string ConvertMixedToMarkdown(string input, bool isLegacy)
    {
        var html = TextValidation.ConvertToHtml(input); // Normalizes to html
        if (isLegacy)
        {
            html = MakeLinksAbsolute(html);
        }

        var config = new Config
        {
            UnknownTags = Config.UnknownTagsOption.Bypass, 
            GithubFlavored = true, 
            RemoveComments = true,
            SmartHrefHandling = false,
            DefaultCodeBlockLanguage = ""
        };
        config.WhitelistUriSchemes.Add("mailto");
        config.WhitelistUriSchemes.Add("http");
        config.WhitelistUriSchemes.Add("https");

        var converter = new Converter(config);
        var processed = converter.Convert(html);
        return processed;
    }

    private static string MakeLinksAbsolute(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return html;
        }

        var baseUri = new Uri("https://altinn.no/");
        const string pattern = "href\\s*=\\s*(\"|')(.*?)\\1";

        return Regex.Replace(
            html,
            pattern,
            match =>
            {
                var quote = match.Groups[1].Value;
                var href = match.Groups[2].Value;

                if (string.IsNullOrWhiteSpace(href))
                {
                    return match.Value;
                }

                if (href.StartsWith("#", StringComparison.Ordinal) ||
                    Uri.TryCreate(href, UriKind.Absolute, out _))
                {
                    return match.Value;
                }

                var absolute = new Uri(baseUri, href).ToString();
                return $"href={quote}{absolute}{quote}";
            },
            RegexOptions.IgnoreCase);
    }
}
