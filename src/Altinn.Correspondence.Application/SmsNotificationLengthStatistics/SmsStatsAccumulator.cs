using System.Globalization;

namespace Altinn.Correspondence.Application.SmsNotificationLengthStatistics;

public class SmsStatsAccumulator
{
    public long CorrespondencesScanned { get; set; }
    public long GenericSmsNotificationsSent { get; set; }

    public long NameLookupFailures { get; set; }
    public long ResourceLookupFailures { get; set; }
    public long SenderLookupFailures { get; set; }
    public long ProcessingFailures { get; set; }

    public SmsLengthStats NewTotals { get; set; } = new();
    public SmsLengthStats NewOrganizationTotals { get; set; } = new();
    public SmsLengthStats NewPersonTotals { get; set; } = new();
    public SmsLengthStats OldTotals { get; set; } = new();
    public SmsLengthStats OldOrganizationTotals { get; set; } = new();
    public SmsLengthStats OldPersonTotals { get; set; } = new();
    public LengthTransitionCounters Transitions { get; set; } = new();
    public LengthSummary RecipientNameLengths { get; set; } = new();
    public LengthSummary ResourceNameLengths { get; set; } = new();
    public LengthSummary MessageTitleLengths { get; set; } = new();
    public LengthSummary SenderNameLengths { get; set; } = new();

    public void RecordSmsNotification(bool isPerson, int oldBodyLength, int newBodyLength)
    {
        GenericSmsNotificationsSent++;

        NewTotals.Record(newBodyLength);
        OldTotals.Record(oldBodyLength);
        (isPerson ? NewPersonTotals : NewOrganizationTotals).Record(newBodyLength);
        (isPerson ? OldPersonTotals : OldOrganizationTotals).Record(oldBodyLength);
        Transitions.Record(oldBodyLength, newBodyLength);
    }

    public void RecordTokenLengths(string? recipientName, string? resourceName, string? messageTitle, string? senderName)
    {
        if (!string.IsNullOrWhiteSpace(recipientName)) RecipientNameLengths.Record(recipientName.Length);
        if (!string.IsNullOrWhiteSpace(resourceName)) ResourceNameLengths.Record(resourceName.Length);
        if (!string.IsNullOrWhiteSpace(messageTitle)) MessageTitleLengths.Record(messageTitle.Length);
        if (!string.IsNullOrWhiteSpace(senderName)) SenderNameLengths.Record(senderName.Length);
    }
}

/// <summary>
/// Length distribution for one set of SMS bodies: how many were measured, their min/max/average
/// length, and how many exceeded the single-segment limit.
/// </summary>
public class SmsLengthStats
{
    public const int SingleSegmentLimit = 160;

    public long Count { get; set; }
    public long SumLength { get; set; }
    public int MinLength { get; set; } = int.MaxValue;
    public int MaxLength { get; set; }
    public long Over160Count { get; set; }

    public double AverageLength => Count == 0 ? 0 : (double)SumLength / Count;
    public double Over160Percent => Count == 0 ? 0 : (double)Over160Count * 100 / Count;

    public void Record(int length)
    {
        Count++;
        SumLength += length;
        if (length < MinLength) MinLength = length;
        if (length > MaxLength) MaxLength = length;
        if (length > SingleSegmentLimit) Over160Count++;
    }

    public string ToLogString()
    {
        if (Count == 0) return "Count=0";
        var min = MinLength == int.MaxValue ? 0 : MinLength;
        return string.Format(
            CultureInfo.InvariantCulture,
            "Count={0} Min={1} Max={2} Avg={3:F1} Over160={4} Over160Pct={5:F1}",
            Count, min, MaxLength, AverageLength, Over160Count, Over160Percent);
    }
}

/// <summary>Min/max/average length of a single substituted token (e.g. recipient name, title).</summary>
public class LengthSummary
{
    public long Samples { get; set; }
    public long SumLength { get; set; }
    public int MinLength { get; set; } = int.MaxValue;
    public int MaxLength { get; set; }

    public double AverageLength => Samples == 0 ? 0 : (double)SumLength / Samples;

    public void Record(int length)
    {
        Samples++;
        SumLength += length;
        if (length < MinLength) MinLength = length;
        if (length > MaxLength) MaxLength = length;
    }

    public string ToLogString()
    {
        if (Samples == 0) return "Samples=0";
        var min = MinLength == int.MaxValue ? 0 : MinLength;
        return string.Format(CultureInfo.InvariantCulture, "Avg={0:F1} Min={1} Max={2} Samples={3}", AverageLength, min, MaxLength, Samples);
    }
}

/// <summary>
/// Cross-tabulation of how each sent SMS would be classified against the 160-character
/// single-segment limit under the old template versus the new template.
/// </summary>
public class LengthTransitionCounters
{
    /// <summary>Both the old and the new templates are kept to a single-segment.</summary>
    public long BothWithin { get; set; }

    /// <summary>Only the old template is kept to a single-segment.</summary>
    public long OnlyOldWithin { get; set; }

    /// <summary>Only the new template is kept to a single-segment.</summary>
    public long OnlyNewWithin { get; set; }

    /// <summary>multi-segment under both new and old templates.</summary>
    public long NeitherWithin { get; set; }

    public long Total() => BothWithin + OnlyOldWithin + OnlyNewWithin + NeitherWithin;

    public void Record(int oldLength, int newLength)
    {
        var oldExceeds = oldLength > SmsLengthStats.SingleSegmentLimit;
        var newExceeds = newLength > SmsLengthStats.SingleSegmentLimit;

        if (!oldExceeds && !newExceeds) BothWithin++;
        else if (!oldExceeds && newExceeds) OnlyOldWithin++;
        else if (oldExceeds && !newExceeds) OnlyNewWithin++;
        else NeitherWithin++;
    }
}
