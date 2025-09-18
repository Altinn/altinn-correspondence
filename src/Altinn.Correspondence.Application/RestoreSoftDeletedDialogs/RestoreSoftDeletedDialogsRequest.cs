using System.ComponentModel.DataAnnotations;

namespace Altinn.Correspondence.Application.RestoreSoftDeletedDialogs;

public class RestoreSoftDeletedDialogsRequest
{
    [Range(100, int.MaxValue)]
    public int WindowSize { get; set; } = 1000;
} 