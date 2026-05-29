using System.Globalization;

namespace Altinn.Correspondence.Application.SmsNotificationLengthStatistics;

public class SmsLengthStats
{
    public long Count { get; private set; }
    public long SumLength { get; private set; }
    public int MinLength { get; private set; } = int.MaxValue;
    public int MaxLength { get; private set; }

    public long NameLookupFailures { get; set; }
    public long ResourceLookupFailures { get; set; }
    public long ProcessingFailures { get; set; }

    public long RecipientNameSamples { get; private set; }
    public long RecipientNameSumLength { get; private set; }
    public int RecipientNameMinLength { get; private set; } = int.MaxValue;
    public int RecipientNameMaxLength { get; private set; }

    public long ResourceNameSamples { get; private set; }
    public long ResourceNameSumLength { get; private set; }
    public int ResourceNameMinLength { get; private set; } = int.MaxValue;
    public int ResourceNameMaxLength { get; private set; }

    private const int MaxTrackedLength = 1000;
    private readonly long[] _lengthBuckets = new long[MaxTrackedLength + 1];

    public void Record(int length)
    {
        Count++;
        SumLength += length;
        if (length < MinLength) MinLength = length;
        if (length > MaxLength) MaxLength = length;

        var bucketIndex = Math.Min(length, MaxTrackedLength);
        _lengthBuckets[bucketIndex]++;
    }

    public void RecordRecipientNameLength(int length)
    {
        RecipientNameSamples++;
        RecipientNameSumLength += length;
        if (length < RecipientNameMinLength) RecipientNameMinLength = length;
        if (length > RecipientNameMaxLength) RecipientNameMaxLength = length;
    }

    public void RecordResourceNameLength(int length)
    {
        ResourceNameSamples++;
        ResourceNameSumLength += length;
        if (length < ResourceNameMinLength) ResourceNameMinLength = length;
        if (length > ResourceNameMaxLength) ResourceNameMaxLength = length;
    }

    public double AverageRecipientNameLength => RecipientNameSamples == 0 ? 0 : (double)RecipientNameSumLength / RecipientNameSamples;
    public double AverageResourceNameLength => ResourceNameSamples == 0 ? 0 : (double)ResourceNameSumLength / ResourceNameSamples;

    public double AverageLength => Count == 0 ? 0 : (double)SumLength / Count;

    public int Percentile(double percentile)
    {
        if (Count == 0) return 0;
        var target = (long)Math.Ceiling(percentile * Count / 100.0);
        long running = 0;
        for (int i = 0; i < _lengthBuckets.Length; i++)
        {
            running += _lengthBuckets[i];
            if (running >= target)
            {
                return i;
            }
        }
        return MaxLength;
    }

    public string ToLogString()
    {
        if (Count == 0) return "Count=0";
        var min = MinLength == int.MaxValue ? 0 : MinLength;
        var ci = CultureInfo.InvariantCulture;
        var lengthBuckets = string.Join(",", new[]
        {
            $"<=100:{SumBuckets(0, 100)}",
            $"101-140:{SumBuckets(101, 140)}",
            $"141-160:{SumBuckets(141, 160)}",
            $"161-200:{SumBuckets(161, 200)}",
            $"201-280:{SumBuckets(201, 280)}",
            $"281+:{SumBuckets(281, MaxTrackedLength)}"
        });
        var recipientNameMin = RecipientNameMinLength == int.MaxValue ? 0 : RecipientNameMinLength;
        var resourceNameMin = ResourceNameMinLength == int.MaxValue ? 0 : ResourceNameMinLength;
        return string.Format(
            ci,
            "TotalCorrespondencesChecked={0} Min={1} Max={2} Avg={3:F1} p50={4} p90={5} p95={6} p99={7} NameFails={8} ResourceFails={9} ProcFails={10} RecipientNameAvg={11:F1} RecipientNameMin={12} RecipientNameMax={13} RecipientNameSamples={14} ResourceNameAvg={15:F1} ResourceNameMin={16} ResourceNameMax={17} ResourceNameSamples={18} LengthBuckets=[{19}]",
            Count, min, MaxLength, AverageLength,
            Percentile(50), Percentile(90), Percentile(95), Percentile(99),
            NameLookupFailures, ResourceLookupFailures, ProcessingFailures,
            AverageRecipientNameLength, recipientNameMin, RecipientNameMaxLength, RecipientNameSamples,
            AverageResourceNameLength, resourceNameMin, ResourceNameMaxLength, ResourceNameSamples,
            lengthBuckets);
    }

    private long SumBuckets(int fromBucket, int toBucket)
    {
        long sum = 0;
        for (int i = fromBucket; i <= toBucket && i < _lengthBuckets.Length; i++)
        {
            sum += _lengthBuckets[i];
        }
        return sum;
    }
}
