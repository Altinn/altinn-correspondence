namespace Altinn.Correspondence.Application.RestoreSoftDeletedDialogs;

public class RestoreSoftDeletedDialogsResponse
{
    public string? JobId { get; set; }
    public string? Message { get; set; }
    public int? TotalProcessed { get; set; }
    public int? TotalAlreadyDeleted { get; set; }
    public int? TotalNotDeleted { get; set; }
    public int? TotalErrors { get; set; }
} 