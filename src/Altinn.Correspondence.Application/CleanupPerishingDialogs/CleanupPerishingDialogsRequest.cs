using System.ComponentModel.DataAnnotations;

namespace Altinn.Correspondence.Application.CleanupPerishingDialogs;

public class CleanupPerishingDialogsRequest
{
    [Range(100, int.MaxValue)]
    public int WindowSize { get; set; } = 10000;
} 