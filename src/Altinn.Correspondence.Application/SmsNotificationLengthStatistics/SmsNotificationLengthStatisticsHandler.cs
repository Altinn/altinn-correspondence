using System.Security.Claims;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Extensions;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.Logging;
using OneOf;

namespace Altinn.Correspondence.Application.SmsNotificationLengthStatistics;

/// <summary>
/// Estimates SMS notification lengths for the chosen time range, comparing the current
/// ("old") GenericAltinnMessage template against a proposed ("new") template.
///
/// The work is split into single-batch Hangfire jobs that chain via a (Created, Id) keyset cursor,
/// carrying the accumulated statistics forward, to avoid the Hangfire job invisibility timeout.
/// </summary>
public class SmsNotificationLengthStatisticsHandler(
    ICorrespondenceRepository correspondenceRepository,
    IAltinnRegisterService altinnRegisterService,
    IResourceRegistryService resourceRegistryService,
    IBackgroundJobClient backgroundJobClient,
    ILogger<SmsNotificationLengthStatisticsHandler> logger) : IHandler<SmsNotificationLengthStatisticsRequest, SmsNotificationLengthStatisticsResponse>
{
    private static readonly DateTimeOffset DefaultFrom = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset DefaultTo = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private const int DefaultBatchSize = 1000;
    private const int DefaultMaxBatches = 10000;
    private const int SingleSegmentLimit = SmsLengthStats.SingleSegmentLimit;

    public Task<OneOf<SmsNotificationLengthStatisticsResponse, Error>> Process(
        SmsNotificationLengthStatisticsRequest request,
        ClaimsPrincipal? user,
        CancellationToken cancellationToken)
    {
        var from = request.From ?? DefaultFrom;
        var to = request.To ?? DefaultTo;
        var batchSize = request.BatchSize is > 0 ? request.BatchSize.Value : DefaultBatchSize;
        var maxBatches = request.MaxBatches is > 0 ? request.MaxBatches.Value : DefaultMaxBatches;

        logger.LogInformation(
            "Enqueuing sms-notification-length-statistics job for range {From} to {To} with batchSize {BatchSize} maxBatches {MaxBatches}",
            from, to, batchSize, maxBatches);

        var jobId = backgroundJobClient.Enqueue<SmsNotificationLengthStatisticsHandler>(
            handler => handler.ExecuteBatch(
                from, to, batchSize, maxBatches,
                null, null,
                1,
                new SmsStatsAccumulator(),
                null,
                CancellationToken.None));

        return Task.FromResult<OneOf<SmsNotificationLengthStatisticsResponse, Error>>(
            new SmsNotificationLengthStatisticsResponse
            {
                JobId = jobId,
                From = from,
                To = to,
                BatchSize = batchSize
            });
    }

    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 1800)]
    public async Task ExecuteBatch(
        DateTimeOffset from,
        DateTimeOffset to,
        int batchSize,
        int maxBatches,
        DateTimeOffset? cursorCreated,
        Guid? cursorId,
        int batchNumber,
        SmsStatsAccumulator accumulator,
        PerformContext? performContext,
        CancellationToken cancellationToken)
    {
        var jobId = performContext?.BackgroundJob?.Id;
        logger.LogInformation(
            "Starting sms-notification-length-statistics batch {BatchNumber} (JobId={JobId}): range {From}..{To} batchSize {BatchSize} cursorCreated={CursorCreated} cursorId={CursorId}",
            batchNumber, jobId, from, to, batchSize, cursorCreated, cursorId);

        var batch = await correspondenceRepository.GetCorrespondencesCreatedInRange(
            from, to, cursorCreated, cursorId, batchSize, cancellationToken);

        if (batch.Count == 0)
        {
            LogCompletion(from, to, batchNumber - 1, accumulator, jobId);
            return;
        }

        var scannedBefore = accumulator.CorrespondencesScanned;
        var smsSentBefore = accumulator.GenericSmsNotificationsSent;

        foreach (var correspondence in batch)
        {
            await ProcessCorrespondence(correspondence, accumulator, cancellationToken);
        }

        logger.LogInformation(
            "SMS length batch {BatchIndex} (JobId={JobId}) scanned {BatchScanned} correspondences, {BatchSmsSent} qualifying SMS. Running totals: CorrespondencesScanned={TotalScanned} SmsGenericNotificationsSent={TotalSmsSent}. New: {NewStats} | Old: {OldStats}",
            batchNumber, jobId, accumulator.CorrespondencesScanned - scannedBefore, accumulator.GenericSmsNotificationsSent - smsSentBefore, accumulator.CorrespondencesScanned, accumulator.GenericSmsNotificationsSent, accumulator.NewTotals.ToLogString(), accumulator.OldTotals.ToLogString());

        if (batch.Count < batchSize)
        {
            LogCompletion(from, to, batchNumber, accumulator, jobId);
            return;
        }

        var last = batch[^1];
        if (batchNumber >= maxBatches)
        {
            logger.LogWarning(
                "sms-notification-length-statistics hit the safety limit of {MaxBatches} batches (JobId={JobId}) before completing. Stopping. Resume by re-running with From=cursorCreated. Resume cursor: cursorCreated={CursorCreated} cursorId={CursorId}. New totals so far: {NewStats} | Old totals so far: {OldStats}",
                maxBatches, jobId, last.Created, last.Id, accumulator.NewTotals.ToLogString(), accumulator.OldTotals.ToLogString());
            return;
        }

        var nextJobId = backgroundJobClient.Enqueue<SmsNotificationLengthStatisticsHandler>(
            handler => handler.ExecuteBatch(
                from, to, batchSize, maxBatches,
                last.Created, last.Id,
                batchNumber + 1,
                accumulator,
                null,
                CancellationToken.None));

        logger.LogInformation(
            "Scheduled next sms-notification-length-statistics batch {NextBatch} as Hangfire job {NextJobId} (cursorCreated={CursorCreated} cursorId={CursorId})",
            batchNumber + 1, nextJobId, last.Created, last.Id);
    }

    private async Task ProcessCorrespondence(
        CorrespondenceEntity correspondence,
        SmsStatsAccumulator accumulator,
        CancellationToken cancellationToken)
    {
        accumulator.CorrespondencesScanned++;
        try
        {
            var smsNotifications = GetSentGenericSmsNotifications(correspondence);
            if (smsNotifications.Count == 0)
            {
                return;
            }

            var isPerson = RecipientCategoryClassifier.Classify(correspondence.RecipientType, correspondence.Recipient) == RecipientCategory.Person;
            var recipientNumber = correspondence.Recipient.WithoutPrefix();

            var recipientName = await ResolveRecipientName(correspondence, recipientNumber, accumulator, cancellationToken);
            var resourceName = await ResolveResourceName(correspondence, accumulator, cancellationToken);
            var senderName = await ResolveSenderName(correspondence, accumulator, cancellationToken);

            var displayRecipient = string.IsNullOrWhiteSpace(recipientName) ? recipientNumber : recipientName;
            var displayResource = string.IsNullOrWhiteSpace(resourceName) ? correspondence.ResourceId : resourceName;
            var displaySender = string.IsNullOrWhiteSpace(senderName) ? correspondence.Sender.WithoutPrefix() : senderName;
            var title = correspondence.Content.MessageTitle;

            var newBody = BuildNewSmsBody(isPerson, recipientNumber, displayRecipient, title, displayResource);

            foreach (var notification in smsNotifications)
            {
                var oldBody = RenderOldGenericSmsBody(notification.IsReminder, displayRecipient, displaySender);
                accumulator.RecordSmsNotification(isPerson, oldBody.Length, newBody.Length);
            }

            accumulator.RecordTokenLengths(recipientName, resourceName, title, senderName);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to compute SMS length for correspondence {CorrespondenceId}", correspondence.Id);
            accumulator.ProcessingFailures++;
        }
    }

    private static List<CorrespondenceNotificationEntity> GetSentGenericSmsNotifications(CorrespondenceEntity correspondence) =>
        correspondence.Notifications
            .Where(n => n.NotificationTemplate == NotificationTemplate.GenericAltinnMessage
                        && n.NotificationSent.HasValue
                        && IsSmsChannel(n.NotificationChannel))
            .ToList();

    /// <summary>Channels that result in an SMS being sent. EmailPreferred is excluded: it only falls
    /// back to SMS when the recipient has no email address, which cannot be determined here.</summary>
    private static bool IsSmsChannel(NotificationChannel channel) =>
        channel is NotificationChannel.Sms or NotificationChannel.SmsPreferred or NotificationChannel.EmailAndSms;

    /// <summary>Builds the proposed new SMS body. Organizations show their (public) organisation
    /// number; persons must not have their national identity number in an SMS, so it is omitted.</summary>
    private static string BuildNewSmsBody(bool isPerson, string recipientNumber, string recipientName, string title, string resourceName)
    {
        var recipientNumberPart = isPerson ? string.Empty : $" ({recipientNumber})";
        return $"{recipientName}{recipientNumberPart} har mottatt «{title}» i Altinn. For å lese kreves tilgang til {resourceName}.";
    }

    /// <summary>Reconstructs the current GenericAltinnMessage SMS body (Norwegian bokmål, no custom
    /// text token), matching how CreateNotificationOrderHandler renders the template sent today.</summary>
    private static string RenderOldGenericSmsBody(bool isReminder, string recipientName, string sendersName)
    {
        var template = isReminder
            ? "Hei. Dette er en påminnelse om at $correspondenceRecipientName$ har mottatt en ny melding fra $sendersName$. {textToken}Logg deg inn i Altinn for å se denne meldingen."
            : "Hei. $correspondenceRecipientName$ har mottatt en ny melding fra $sendersName$. {textToken}Logg deg inn i Altinn for å se denne meldingen.";

        return template
            .Replace("{textToken}", "")
            .Trim()
            .Replace("$correspondenceRecipientName$", recipientName)
            .Replace("$sendersName$", sendersName);
    }

    private async Task<string?> ResolveRecipientName(CorrespondenceEntity correspondence, string recipientNumber, SmsStatsAccumulator accumulator, CancellationToken cancellationToken)
    {
        try
        {
            return (await altinnRegisterService.LookUpPartyById(recipientNumber, cancellationToken))?.GetDisplayName();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LookUpName failed for correspondence {CorrespondenceId}", correspondence.Id);
            accumulator.NameLookupFailures++;
            return null;
        }
    }

    private async Task<string?> ResolveResourceName(CorrespondenceEntity correspondence, SmsStatsAccumulator accumulator, CancellationToken cancellationToken)
    {
        try
        {
            return await resourceRegistryService.GetResourceTitle(correspondence.ResourceId, "nb", cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GetResourceTitle failed for resource {ResourceId}", correspondence.ResourceId);
            accumulator.ResourceLookupFailures++;
            return null;
        }
    }

    /// <summary>Resolves the sender name used by the old template, preferring the correspondence's
    /// explicit MessageSender and falling back to a register lookup of the sender.</summary>
    private async Task<string?> ResolveSenderName(CorrespondenceEntity correspondence, SmsStatsAccumulator accumulator, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(correspondence.MessageSender))
        {
            return correspondence.MessageSender;
        }
        try
        {
            return (await altinnRegisterService.LookUpPartyById(correspondence.Sender, cancellationToken))?.GetDisplayName();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LookUpSenderName failed for correspondence {CorrespondenceId}", correspondence.Id);
            accumulator.SenderLookupFailures++;
            return null;
        }
    }

    private void LogCompletion(DateTimeOffset from, DateTimeOffset to, int batchesProcessed, SmsStatsAccumulator accumulator, string? jobId)
    {
        logger.LogInformation(
            "Completed sms-notification-length-statistics for range {From}..{To} (JobId={JobId}). Batches={Batches}. CorrespondencesScanned={Scanned} SmsGenericNotificationsSent={SmsSent} (single-segment limit={SegmentLimit}).",
            from, to, jobId, batchesProcessed, accumulator.CorrespondencesScanned, accumulator.GenericSmsNotificationsSent, SingleSegmentLimit);

        logger.LogInformation(
            "sms-notification-length-statistics SUMMARY (JobId={JobId}) range {From}..{To}: GenericAltinnMessage SMS sent={SmsSent}. Bodies exceeding {SegmentLimit} chars (multi-segment) — NEW template: {NewOver160} ({NewOver160Pct:F1}%), OLD template: {OldOver160} ({OldOver160Pct:F1}%).",
            jobId, from, to, accumulator.GenericSmsNotificationsSent, SingleSegmentLimit,
            accumulator.NewTotals.Over160Count, accumulator.NewTotals.Over160Percent,
            accumulator.OldTotals.Over160Count, accumulator.OldTotals.Over160Percent);

        var transitions = accumulator.Transitions;
        logger.LogInformation(
            "sms-notification-length-statistics TRANSITION (JobId={JobId}): GenericAltinnMessage SMS sent={Total}. " +
            "OnlyOldWithin (old<=160 -> new>160)={OnlyOldWithin}. BothWithin (old<=160 -> new<=160)={BothWithin}. " +
            "OnlyNewWithin (old>160 -> new<=160)={OnlyNewWithin}. NeitherWithin (old>160 -> new>160)={NeitherWithin}.",
            jobId, transitions.Total(),
            transitions.OnlyOldWithin, transitions.BothWithin, transitions.OnlyNewWithin, transitions.NeitherWithin);

        logger.LogInformation("sms-notification-length-statistics NEW template Totals: {Stats}", accumulator.NewTotals.ToLogString());
        logger.LogInformation("sms-notification-length-statistics NEW template Organization: {Stats}", accumulator.NewOrganizationTotals.ToLogString());
        logger.LogInformation("sms-notification-length-statistics NEW template Person: {Stats}", accumulator.NewPersonTotals.ToLogString());

        logger.LogInformation("sms-notification-length-statistics OLD template Totals: {Stats}", accumulator.OldTotals.ToLogString());
        logger.LogInformation("sms-notification-length-statistics OLD template Organization: {Stats}", accumulator.OldOrganizationTotals.ToLogString());
        logger.LogInformation("sms-notification-length-statistics OLD template Person: {Stats}", accumulator.OldPersonTotals.ToLogString());

        logger.LogInformation(
            "sms-notification-length-statistics TOKEN LENGTHS (JobId={JobId}): RecipientName [{Recipient}] ResourceName [{Resource}] MessageTitle [{Title}] SenderName [{Sender}].",
            jobId,
            accumulator.RecipientNameLengths.ToLogString(),
            accumulator.ResourceNameLengths.ToLogString(),
            accumulator.MessageTitleLengths.ToLogString(),
            accumulator.SenderNameLengths.ToLogString());

        logger.LogInformation(
            "sms-notification-length-statistics FAILURES (JobId={JobId}): NameLookup={NameFails} ResourceLookup={ResourceFails} SenderLookup={SenderFails} Processing={ProcFails}.",
            jobId, accumulator.NameLookupFailures, accumulator.ResourceLookupFailures, accumulator.SenderLookupFailures, accumulator.ProcessingFailures);
    }
}
