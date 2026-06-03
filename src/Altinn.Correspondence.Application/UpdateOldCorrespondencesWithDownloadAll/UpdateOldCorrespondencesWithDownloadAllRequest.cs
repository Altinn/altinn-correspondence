using System.ComponentModel.DataAnnotations;

namespace Altinn.Correspondence.Application.UpdateOldCorrespondencesWithDownloadAll;

public class UpdateOldCorrespondencesWithDownloadAllRequest
{
    [Range(100, int.MaxValue)]
    public int windowSize { get; set; } = 10000;

}