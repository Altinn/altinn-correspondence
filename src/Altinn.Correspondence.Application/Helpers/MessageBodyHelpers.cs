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
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var preprocessed = isLegacy ? MakeLinksAbsolute(input) : input;
        var html = TextValidation.ConvertToHtml(preprocessed); // Normalizes to html

        var config = new Config
        {
            UnknownTags = Config.UnknownTagsOption.Bypass, 
            GithubFlavored = true, 
            RemoveComments = true,
            SmartHrefHandling = false,
            DefaultCodeBlockLanguage = "",
            WhitelistUriSchemes = [
                "mailto",
                "http",
                "https"
                ]
        };

        var converter = new Converter(config);
        var processed = converter.Convert(html);
        return processed;
    }

    private static string MakeLinksAbsolute(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var baseUri = new Uri("https://altinn.no/");
        const string htmlHrefPattern = "href\\s*=\\s*(\"|')(.*?)\\1";
        const string markdownLinkPattern = "\\[(?<text>[^\\]]+)\\]\\((?<url>[^)]+)\\)";

        var htmlProcessed = Regex.Replace(
            input,
            htmlHrefPattern,
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

        return Regex.Replace(
            htmlProcessed,
            markdownLinkPattern,
            match =>
            {
                var text = match.Groups["text"].Value;
                var url = match.Groups["url"].Value;

                if (string.IsNullOrWhiteSpace(url))
                {
                    return match.Value;
                }

                if (url.StartsWith("#", StringComparison.Ordinal) ||
                    Uri.TryCreate(url, UriKind.Absolute, out _))
                {
                    return match.Value;
                }

                var absolute = new Uri(baseUri, url).ToString();
                return $"[{text}]({absolute})";
            },
            RegexOptions.IgnoreCase);
    }
}
