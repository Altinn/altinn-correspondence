using System.ComponentModel.DataAnnotations;

namespace Altinn.Correspondence.Application.UpdateOldCorrespondencesWithDownloadAll;

public class UpdateOldCorrespondencesWithDownloadAllRequest
{
    [Range(100, int.MaxValue - 1)]
    public int windowSize { get; set; } = 10000;

}