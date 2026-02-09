using System.ComponentModel.DataAnnotations;

namespace Altinn.Correspondence.Application.CleanupBruksmonster;

public class CleanupBruksmonsterRequest
{

    [Range(0, 365)]
    public int? MinAgeDays { get; init; }
} 