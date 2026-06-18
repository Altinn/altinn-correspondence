using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Application.DownloadAllCorrespondenceAttachments;

public class DownloadAllCorrespondenceAttachmentsResponse
{
    /// <summary>
    /// The attachments to include in the zip, paired with the (deduplicated, length-bounded) entry name to use.
    /// Resolved during validation so the zip can be streamed straight to the response without buffering.
    /// </summary>
    public required IReadOnlyList<ZipAttachmentEntry> Entries { get; set; }
    public required string ZipFileName { get; set; }
}

public record ZipAttachmentEntry(AttachmentEntity Attachment, string EntryName);
