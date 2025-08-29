using System.ComponentModel.DataAnnotations;

namespace Altinn.Correspondence.Application.CleanupOrphanedDialogs;

public class CleanupOrphanedDialogsRequest
{
    [Range(100, int.MaxValue)]
    public int WindowSize { get; set; } = 10000;
}


