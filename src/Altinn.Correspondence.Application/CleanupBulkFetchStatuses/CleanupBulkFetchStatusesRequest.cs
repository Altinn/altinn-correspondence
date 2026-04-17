using System.ComponentModel.DataAnnotations;

namespace Altinn.Correspondence.Application.CleanupBulkFetchStatuses;

public class CleanupBulkFetchStatusesRequest
{
    [Range(1, int.MaxValue)]
    public int WindowSize { get; set; } = 5;
} 
