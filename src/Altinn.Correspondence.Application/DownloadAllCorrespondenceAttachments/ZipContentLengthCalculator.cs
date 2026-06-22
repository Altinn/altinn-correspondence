using System.Text;

namespace Altinn.Correspondence.Application.DownloadAllCorrespondenceAttachments;

/// <summary>
/// Computes the exact byte length of the uncompressed (stored) zip produced by
/// <see cref="DownloadAllCorrespondenceAttachmentsHandler.WriteZip"/>, so a Content-Length can be set on
/// the streamed response.
/// </summary>
/// <remarks>
/// The overhead constants are the ZIP format's fixed structure for a STORED entry written to a
/// non-seekable stream. The formula is only valid below the zip64 boundary (4 GiB); above it the archive switches format and
/// the overhead changes, so the caller must fall back to chunked transfer (no Content-Length).
/// It also assumes each attachment's recorded size matches its actual stored bytes.
/// </remarks>
public static class ZipContentLengthCalculator
{
    private const long PerEntryOverhead = 76;       // local file header (30) + central directory header (46)
    private const long DataDescriptorOverhead = 16; // trailing data descriptor, only emitted for entries with content
    private const long PerNameByteOverhead = 2;     // the file name is stored in both the local header and the central directory
    private const long EndOfCentralDirectory = 22;

    // At or above these values a field no longer fits in 32 bits and ZipArchive emits zip64, invalidating the formula.
    private const long Zip64SizeThreshold = 0xFFFFFFFF;   // 4 GiB - 1 (covers per-entry size, header offsets and total size)
    private const int Zip64EntryCountThreshold = 0xFFFF;  // 65535

    /// <summary>
    /// Returns the exact length of the stored zip, or null if it would cross the zip64 boundary
    /// </summary>
    public static long? TryCalculate(IReadOnlyList<ZipAttachmentEntry> entries)
    {
        if (entries.Count >= Zip64EntryCountThreshold)
        {
            return null;
        }

        long total = EndOfCentralDirectory;
        foreach (var entry in entries)
        {
            var size = entry.Attachment.AttachmentSize;
            total += size
                   + PerEntryOverhead
                   + (size > 0 ? DataDescriptorOverhead : 0)
                   + PerNameByteOverhead * Encoding.UTF8.GetByteCount(entry.EntryName);
        }

        return total >= Zip64SizeThreshold ? null : total;
    }
}
