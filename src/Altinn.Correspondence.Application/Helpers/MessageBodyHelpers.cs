using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Altinn.Correspondence.Common.Helpers;
using ReverseMarkdown;

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
        var links = ExtractLinks(preprocessed);
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

        // Ensure that all discovered links are present in the final markdown,
        // even if ReverseMarkdown drops them in some environments.
        foreach (var link in links)
        {
            if (!string.IsNullOrEmpty(link) &&
                processed.IndexOf(link, StringComparison.Ordinal) < 0)
            {
                processed += Environment.NewLine + link;
            }
        }

        return processed;
    }

    private static string MakeLinksAbsolute(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var baseUri = new Uri("https://altinn.no/");
        // Only operate on anchor tags, not other elements like <link>
        const string htmlHrefPattern = "<a\\b[^>]*?href\\s*=\\s*(\"|')(.*?)\\1";
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

    private static IEnumerable<string> ExtractLinks(string input)
    {
        var links = new HashSet<string>(StringComparer.Ordinal);

        if (string.IsNullOrEmpty(input))
        {
            return links;
        }

        // Only extract hrefs from anchor tags
        const string htmlHrefPattern = "<a\\b[^>]*?href\\s*=\\s*(\"|')(.*?)\\1";
        const string markdownLinkPattern = "\\[(?<text>[^\\]]+)\\]\\((?<url>[^)]+)\\)";

        var baseUri = new Uri("https://altinn.no/");

        foreach (Match match in Regex.Matches(input, htmlHrefPattern, RegexOptions.IgnoreCase))
        {
            var href = match.Groups[2].Value;
            if (!string.IsNullOrWhiteSpace(href))
            {
                // Normalize relative URLs to absolute
                if (!Uri.TryCreate(href, UriKind.Absolute, out var absolute))
                {
                    absolute = new Uri(baseUri, href);
                }

                links.Add(absolute.ToString());
            }
        }

        foreach (Match match in Regex.Matches(input, markdownLinkPattern, RegexOptions.IgnoreCase))
        {
            var url = match.Groups["url"].Value;
            if (!string.IsNullOrWhiteSpace(url))
            {
                // Normalize relative URLs to absolute
                if (!Uri.TryCreate(url, UriKind.Absolute, out var absolute))
                {
                    absolute = new Uri(baseUri, url);
                }

                links.Add(absolute.ToString());
            }
        }

        return links;
    }
}
