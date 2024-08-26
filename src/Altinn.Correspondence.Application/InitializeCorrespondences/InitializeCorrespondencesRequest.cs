using Altinn.Correspondence.Core.Models;
using Microsoft.AspNetCore.Http;

namespace Altinn.Correspondence.Application.InitializeCorrespondences;

public class InitializeCorrespondencesRequest
{
    public required CorrespondenceEntity Correspondence { get; set; }

    public List<IFormFile> Attachments { get; set; } = new List<IFormFile>();

    public bool IsUploadRequest { get; set; }

    public List<Guid> ExistingAttachments { get; set; }

    public List<string> Recipients { get; set; }
}