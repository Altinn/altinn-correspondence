using ReverseMarkdown;
using System.Text.RegularExpressions;
using Altinn.Correspondence.Common.Helpers;

namespace Altinn.Correspondence.Application.Helpers;

public static class MessageBodyHelpers
{
    // Altinn 2 inbox rendered both html and markdown, hence we must do same
    public static string ConvertMixedToMarkdown(string input)
    {
        var html = TextValidation.ConvertToHtml(input); // Normalizes to html
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
}
