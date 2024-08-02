using Altinn.Correspondence.Core.Models;
using Microsoft.AspNetCore.Http;

namespace Altinn.Correspondence.Application.InitializeCorrespondence;

public class InitializeCorrespondenceRequest
{
    public required CorrespondenceEntity Correspondence { get; set; }

    public List<IFormFile> Attachments { get; set; } = new List<IFormFile>();
}
