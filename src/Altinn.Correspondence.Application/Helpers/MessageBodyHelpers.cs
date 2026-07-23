using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Altinn.Correspondence.Common.Helpers;
using ReverseMarkdown;

namespace Altinn.Correspondence.Application.Helpers;

public static class MessageBodyHelpers
{
    // Altinn 2 inbox rendered both html and markdown, hence we must do same
    public static string ConvertMixedToMarkdown(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }
        var preprocessed = input;
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

    private static IEnumerable<string> ExtractLinks(string input)
    {
        var links = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(input)) return links;

        const string htmlHrefPattern = "<a\\b[^>]*?href\\s*=\\s*(\"|')(.*?)\\1";
        const string markdownLinkPattern = "\\[(?<text>[^\\]]+)\\]\\((?<url>[^)]+)\\)";

        void AddUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            links.Add(url);
        }

        foreach (Match m in Regex.Matches(input, htmlHrefPattern, RegexOptions.IgnoreCase))
            AddUrl(m.Groups[2].Value);

        foreach (Match m in Regex.Matches(input, markdownLinkPattern, RegexOptions.IgnoreCase))
            AddUrl(m.Groups["url"].Value);

        return links;
    }
}
