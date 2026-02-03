using System.ComponentModel.DataAnnotations;

namespace Altinn.Correspondence.Application.CleanupBruksmonster;

public class CleanupBruksmonsterRequest
{
    public Guid? TestRunId { get; init; }
    public int? MinAgeDays { get; init; }
} 