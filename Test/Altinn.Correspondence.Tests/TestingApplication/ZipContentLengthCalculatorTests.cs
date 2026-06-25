using System.IO.Compression;
using Altinn.Correspondence.Application.DownloadAllCorrespondenceAttachments;
using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Tests.TestingApplication;

/// <summary>
/// Guards <see cref="ZipContentLengthCalculator"/> against the framework: it builds a real stored zip via
/// the same async ZipArchive path WriteZip uses and asserts the calculated length matches the actual bytes
/// to the byte. If a future .NET version changes the zip writer's overhead, these tests fail in CI pipeline before a
/// wrong Content-Length can corrupt downloads.
/// </summary>
public class ZipContentLengthCalculatorTests
{
    [Fact]
    public async Task TryCalculate_MatchesActualStoredZipLength()
    {
        ZipAttachmentEntry[][] configs =
        [
            [Entry("a.txt", 0)],
            [Entry("a.txt", 1000)],
            [Entry("a.txt", 1000), Entry("b.bin", 500_000)],
            [Entry("file with spaces.pdf", 12345), Entry("æøå-unicode-navn.txt", 67890), Entry("c", 1)],
            [.. Enumerable.Range(0, 50).Select(i => Entry($"attachment_{i}.dat", 1000 + i))],
        ];

        foreach (var entries in configs)
        {
            long actual = await ActualStoredZipLength(entries);
            long? computed = ZipContentLengthCalculator.TryCalculate(entries);
            Assert.Equal(actual, computed);
        }
    }

    [Fact]
    public void TryCalculate_ReturnsNull_WhenEntryCountReachesZip64Boundary()
    {
        var entries = Enumerable.Range(0, 0xFFFF).Select(_ => Entry("f", 1)).ToArray();
        Assert.Null(ZipContentLengthCalculator.TryCalculate(entries));
    }

    [Fact]
    public void TryCalculate_ReturnsNull_WhenTotalSizeReachesZip64Boundary()
    {
        var entries = new[] { Entry("big.bin", 0xFFFFFFFF) };
        Assert.Null(ZipContentLengthCalculator.TryCalculate(entries));
    }

    [Fact]
    public void TryCalculate_ReturnsValue_JustBelowZip64Boundary()
    {
        var entries = new[] { Entry("f", 0xFFFFFFFFL - 1000) };
        Assert.NotNull(ZipContentLengthCalculator.TryCalculate(entries));
    }

    private static ZipAttachmentEntry Entry(string name, long size) =>
        new(new AttachmentEntity
        {
            ResourceId = "test",
            SendersReference = "ref",
            Sender = "0192:000000000",
            Created = DateTimeOffset.UtcNow,
            FileName = name,
            AttachmentSize = size
        }, name);

    private static async Task<long> ActualStoredZipLength(IReadOnlyList<ZipAttachmentEntry> entries)
    {
        var counter = new CountingStream();
        await using (var archive = await ZipArchive.CreateAsync(counter, ZipArchiveMode.Create, leaveOpen: true, entryNameEncoding: null))
        {
            foreach (var (attachment, entryName) in entries)
            {
                var entry = archive.CreateEntry(entryName, CompressionLevel.NoCompression);
                await using var entryStream = await entry.OpenAsync();
                var buffer = new byte[81920];
                long written = 0;
                while (written < attachment.AttachmentSize)
                {
                    int n = (int)Math.Min(buffer.Length, attachment.AttachmentSize - written);
                    await entryStream.WriteAsync(buffer.AsMemory(0, n));
                    written += n;
                }
            }
        }
        return counter.Count;
    }

    /// <summary>Non-seekable stream that just counts bytes written, mimicking the HTTP response body.</summary>
    private sealed class CountingStream : Stream
    {
        public long Count { get; private set; }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => Count; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override void Write(byte[] buffer, int offset, int count) => Count += count;
        public override void Write(ReadOnlySpan<byte> buffer) => Count += buffer.Length;
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) { }
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Count += buffer.Length;
            return ValueTask.CompletedTask;
        }
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Count += count;
            return Task.CompletedTask;
        }
        public override ValueTask DisposeAsync() => default;
    }
}
