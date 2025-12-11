using System.ComponentModel.DataAnnotations;

namespace Altinn.Correspondence.Application.CleanupConfirmedMigratedCorrespondences;

public class CleanupConfirmedMigratedCorrespondencesRequest
{
    [Range(100, int.MaxValue)]
    public int WindowSize { get; set; } = 10000;
} 