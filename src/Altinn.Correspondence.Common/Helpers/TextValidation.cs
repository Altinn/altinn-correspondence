using System;
using System.Net;
using System.Text.RegularExpressions;
using Markdig;

namespace Altinn.Correspondence.Application.Helpers;

public class TextValidation
{
    public static string ConvertToHtml(string markdown)
    {
        var pipleline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseYamlFrontMatter()
            .Build();
        var html = Markdown.ToHtml(markdown, pipleline);
        return html;
    }

    public static bool ValidatePlainText(string text)
    {
        var converter = new ReverseMarkdown.Converter();
        var markdown = converter.Convert(text);
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        var plaintext = Markdown.ToPlainText(markdown, pipeline);
        return plaintext.Trim() == text.Trim();
    }

    public static bool ValidateMarkdown(string markdown)
    {
        var config = new ReverseMarkdown.Config
        {
            CleanupUnnecessarySpaces = false,
            PassThroughTags = new String[] { "br" },
        };
        var converter = new ReverseMarkdown.Converter(config);
        // change all codeblocks to <code> to keep html content in codeblocks
        var markdownWithCodeBlocks = ReplaceMarkdownCodeWithHtmlCode(markdown);
        string result = converter.Convert(markdownWithCodeBlocks);

        // needs to decode the text twice as some encoded characters contains encoded characters, such as emdash &#8212;
        var text = WebUtility.HtmlDecode(WebUtility.HtmlDecode(markdown));
        result = WebUtility.HtmlDecode(WebUtility.HtmlDecode(result));

        //As reversemarkdown makes all code blocks to ` we need to replace ``` with ` and `` with ` to compare the strings
        return ReplaceWhitespaceAndEscapeCharacters(text.Replace("```", "`").Replace("``", "`")) == ReplaceWhitespaceAndEscapeCharacters(result.Replace("```", "`").Replace("``", "`"));
    }

    public static string ReplaceWhitespaceAndEscapeCharacters(string text)
    {
        return Regex.Replace(text, @"\s+", "").Replace("\\", "").ToLower();
    }

    public static string ReplaceMarkdownCodeWithHtmlCode(string text)
    {
        var codeTagsContent = new List<List<string>>();
        var validCodeTagDelimiters = new List<string> { "```", "``", "`" };
        var newText = text;
        var i = 0;
        foreach (var delimiter in validCodeTagDelimiters)
        {
            var counter = 0;
            var markdownWithCodeBlocks = newText.Split(delimiter);
            var tagList = new List<string>();
            newText = "";
            for (var j = 0; j < markdownWithCodeBlocks.Length; j++)
            {
                if (j % 2 == 1)
                {
                    newText += "<---CODE" + i + counter + "--->";
                    tagList.Add(markdownWithCodeBlocks[j].Replace("<", "&lt;").Replace(">", "&gt;"));
                    counter++;
                }
                else newText += markdownWithCodeBlocks[j];
            }
            codeTagsContent.Add(tagList);
            i++;
        }
        for (var j = 0; j < 3; j++)
        {
            var counter = 0;
            foreach (var t in codeTagsContent[j])
            {
                newText = newText.Replace("<---CODE" + j + counter + "--->", "<code>" + t + "</code>");
                counter++;
            }
        }
        return newText;
    }
}
