using System.ComponentModel.DataAnnotations;

namespace Altinn.Correspondence.Application.CleanupMarkdownAndHTMLInSummary;

public class CleanupMarkdownAndHTMLInSummaryRequest
{
    [Range(100, int.MaxValue)]
    public int WindowSize { get; set; } = 10000;
} 