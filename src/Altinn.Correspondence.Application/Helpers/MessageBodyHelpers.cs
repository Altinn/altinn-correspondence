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
        var links = ExtractLinks(preprocessed, isLegacy);
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
        // Only operate on href values inside anchor tags, not other elements like <link>
        // Groups:
        // 1: "<a ... href="
        // 2: the quote character
        // 3: the href value
        // 4: the closing quote
        const string htmlHrefPattern = "(<a\\b[^>]*?href\\s*=\\s*(\"|'))(.*?)(\\2)";
        const string markdownLinkPattern = "\\[(?<text>[^\\]]+)\\]\\((?<url>[^)]+)\\)";

        var htmlProcessed = Regex.Replace(
            input,
            htmlHrefPattern,
            match =>
            {
                var prefix = match.Groups[1].Value;   // "<a ... href="
                var quote = match.Groups[2].Value;    // opening quote
                var href = match.Groups[3].Value;     // current href value
                var closingQuote = match.Groups[4].Value; // closing quote (same as opening)

                if (string.IsNullOrWhiteSpace(href))
                {
                    return match.Value;
                }

                if (href.StartsWith("#", StringComparison.Ordinal) ||
                    IsAbsoluteWebLikeUrl(href))
                {
                    return match.Value;
                }

                var absolute = new Uri(baseUri, href).ToString();
                return $"{prefix}{absolute}{closingQuote}";
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
                    IsAbsoluteWebLikeUrl(url))
                {
                    return match.Value;
                }

                var absolute = new Uri(baseUri, url).ToString();
                return $"[{text}]({absolute})";
            },
            RegexOptions.IgnoreCase);
    }

    private static IEnumerable<string> ExtractLinks(string input, bool isLegacy)
    {
        var links = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(input)) return links;

        const string htmlHrefPattern = "<a\\b[^>]*?href\\s*=\\s*(\"|')(.*?)\\1";
        const string markdownLinkPattern = "\\[(?<text>[^\\]]+)\\]\\((?<url>[^)]+)\\)";
        var baseUri = new Uri("https://altinn.no/");

        void AddUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            if (isLegacy && !IsAbsoluteWebLikeUrl(url))
            {
                url = new Uri(baseUri, url).ToString();
            }

            links.Add(url);
        }

        foreach (Match m in Regex.Matches(input, htmlHrefPattern, RegexOptions.IgnoreCase))
            AddUrl(m.Groups[2].Value);

        foreach (Match m in Regex.Matches(input, markdownLinkPattern, RegexOptions.IgnoreCase))
            AddUrl(m.Groups["url"].Value);

        return links;
    }

    private static bool IsAbsoluteWebLikeUrl(string url)
    {
        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
               || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
               || url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);
    }
}
