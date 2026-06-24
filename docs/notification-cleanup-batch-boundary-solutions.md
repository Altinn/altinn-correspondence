# Batch Boundary Problem & Solutions

## Problem Description

The current batch processing implementation uses `NotificationSent` as a cursor value:

```csharp
// Current query
.Where(n => n.NotificationSent < lastProcessed)
.OrderByDescending(n => n.NotificationSent)
.Take(count)
```

**Risk**: If multiple notifications share the exact same `NotificationSent` timestamp and they span across a batch boundary, some notifications will be **skipped**.

### Example Scenario

Assume batch size = 100:

```
Batch 1 (100 items):
  NotificationSent = 2024-01-15 10:00:00 (85 notifications)
  NotificationSent = 2024-01-15 09:00:00 (15 notifications)

Next batch cursor: 2024-01-15 09:00:00

Batch 2 query: WHERE NotificationSent < '2024-01-15 09:00:00'
❌ SKIPPED: The 15 notifications with timestamp 2024-01-15 09:00:00 are never processed!
```

## Diagnostic Queries

Run these queries to assess the risk in your database (added to `notification-cleanup-monitoring.sql`):

1. **Query 12**: Find all duplicate timestamps
2. **Query 13**: Count total affected notifications  
3. **Query 14**: Find high-risk cases (many notifications with same timestamp)

## Solution Options

### Option 1: Use Composite Cursor (NotificationSent + Id) ⭐ RECOMMENDED

Use both `NotificationSent` and `Id` as a composite cursor to ensure deterministic ordering.

**Advantages:**
- ✅ Completely eliminates skipping risk
- ✅ Deterministic ordering (timestamp + unique ID)
- ✅ No data migration required
- ✅ Works with existing index (may need to add `Id` to index for best performance)

**Implementation:**

```csharp
// Repository method signature
Task<List<CorrespondenceNotificationEntity>> GetSyncedNotificationsWithoutDialogActivityBatch(
	int count, 
	DateTimeOffset lastProcessedTimestamp,
	Guid? lastProcessedId,  // NEW: Add ID parameter
	CancellationToken cancellationToken);

// Repository implementation
public async Task<List<CorrespondenceNotificationEntity>> GetSyncedNotificationsWithoutDialogActivityBatch(
	int count, 
	DateTimeOffset lastProcessedTimestamp,
	Guid? lastProcessedId,
	CancellationToken cancellationToken)
{
	var query = _context.CorrespondenceNotifications
		.Where(n => n.Altinn2NotificationId != null 
				 && n.SyncedFromAltinn2 != null
				 && (n.NotificationSent < lastProcessedTimestamp
					 || (n.NotificationSent == lastProcessedTimestamp && n.Id < lastProcessedId)))
		.OrderByDescending(n => n.NotificationSent)
		.ThenByDescending(n => n.Id);  // Secondary sort by Id

	return await query.Take(count).ToListAsync(cancellationToken);
}

// Handler changes
public async Task Process(int batchCount, DateTimeOffset lastProcessedTimestamp, Guid? lastProcessedId = null)
{
	// ... get batch ...

	if (batch.Count == 0)
	{
		return; // Done
	}

	// Get the oldest notification in batch for next cursor
	var oldestNotification = batch
		.OrderBy(n => n.NotificationSent)
		.ThenBy(n => n.Id)
		.First();

	// Enqueue jobs...

	// Enqueue next batch with composite cursor
	backgroundJobClient.Enqueue<MigrateNotificationEventsBatchHandler>(
		HangfireQueues.Migration, 
		handler => handler.Process(
			batchCount, 
			oldestNotification.NotificationSent.Value,
			oldestNotification.Id));
}
```

**Index Consideration:**
```sql
-- Optimal index for composite cursor (if current index isn't sufficient)
CREATE INDEX CONCURRENTLY IX_CorrespondenceNotifications_Cleanup_Composite 
ON correspondence."CorrespondenceNotifications" ("NotificationSent" DESC, "Id" DESC)
WHERE "Altinn2NotificationId" IS NOT NULL 
  AND "SyncedFromAltinn2" IS NOT NULL;
```

---

### Option 2: Use `<= lastProcessed` with Deduplication

Process all notifications with `NotificationSent <= lastProcessed` but track processed IDs to avoid duplicates.

**Advantages:**
- ✅ Simple query logic
- ✅ Uses existing index

**Disadvantages:**
- ❌ Requires persistent state (processed IDs cache/database)
- ❌ More complex state management
- ❌ Could process same timestamp multiple times

**Implementation:**

```csharp
public async Task Process(
	int batchCount, 
	DateTimeOffset lastProcessed,
	HashSet<Guid>? processedIds = null)  // Track already-processed IDs
{
	processedIds ??= new HashSet<Guid>();

	var batch = await notificationRepository.GetBatch(batchCount + 100, lastProcessed);

	// Filter out already-processed notifications
	var unprocessedBatch = batch
		.Where(n => !processedIds.Contains(n.Id))
		.Take(batchCount)
		.ToList();

	if (unprocessedBatch.Count == 0)
	{
		return; // Done
	}

	// Process and track IDs
	foreach (var notification in unprocessedBatch)
	{
		processedIds.Add(notification.Id);
		// Enqueue job...
	}

	// Find next cursor - need to handle timestamp boundary carefully
	var oldestInBatch = unprocessedBatch.Min(n => n.NotificationSent);
	var notificationsAtBoundary = batch.Where(n => n.NotificationSent == oldestInBatch).ToList();

	foreach (var n in notificationsAtBoundary)
	{
		processedIds.Add(n.Id);
	}

	// Enqueue next batch
	// Problem: How to pass processedIds? Would need serialization or persistent storage
}
```

**Not recommended** due to complexity of state management across Hangfire jobs.

---

### Option 3: Process in Smaller Sub-Batches with Full Timestamp Sweep

For each timestamp, process ALL notifications with that timestamp before moving to the next.

**Advantages:**
- ✅ Guarantees no skips
- ✅ Natural grouping by timestamp

**Disadvantages:**
- ❌ Variable batch sizes
- ❌ Could have very large batches if many notifications share timestamp
- ❌ Less predictable throughput

**Implementation:**

```csharp
public async Task Process(int targetBatchSize, DateTimeOffset lastProcessed)
{
	// Get a batch
	var batch = await notificationRepository.GetBatch(targetBatchSize, lastProcessed);

	if (batch.Count == 0)
	{
		return;
	}

	// Find the oldest timestamp in batch
	var oldestTimestamp = batch.Min(n => n.NotificationSent);

	// Get ALL notifications with that oldest timestamp (could be more than batch size!)
	var completeTimestampBatch = await notificationRepository
		.GetAllNotificationsWithTimestamp(oldestTimestamp);

	// Process complete timestamp batch
	foreach (var notification in completeTimestampBatch)
	{
		// Enqueue job...
	}

	// Next batch starts BEFORE this timestamp (< instead of <=)
	backgroundJobClient.Enqueue<MigrateNotificationEventsBatchHandler>(
		HangfireQueues.Migration, 
		handler => handler.Process(targetBatchSize, oldestTimestamp));
}
```

**Not ideal** due to unpredictable batch sizes.

---

### Option 4: Accept the Risk (with Monitoring)

If duplicate timestamps are rare in your data, accept the small risk.

**Prerequisites:**
1. Run diagnostic queries (added to monitoring.sql)
2. Confirm duplicates are rare and small in count
3. Add monitoring to detect skipped notifications
4. Plan for manual cleanup of any skipped notifications

**Advantages:**
- ✅ No code changes needed
- ✅ Simple to understand

**Disadvantages:**
- ❌ May skip notifications
- ❌ Requires manual intervention if problems occur
- ❌ Not recommended for production-critical systems

---

## Recommendation

**Option 1 (Composite Cursor)** is the best solution because:

1. ✅ **Correctness**: Guarantees no notifications are skipped
2. ✅ **Performance**: Works with existing index, predictable query performance
3. ✅ **Simplicity**: Clean code, no complex state management
4. ✅ **Deterministic**: Same query always returns same results for same cursor
5. ✅ **Scalable**: Handles any data distribution

The implementation requires:
- Updating the repository method signature
- Updating the handler to pass both timestamp and ID
- Updating the handler's Hangfire job signature
- (Optional) Adding composite index for optimal performance

## Next Steps

1. **Assess Risk**: Run queries 12-14 from `notification-cleanup-monitoring.sql`
2. **Choose Solution**: Based on risk assessment, choose Option 1 (recommended) or Option 4 (if risk is negligible)
3. **Implement**: Update code if going with Option 1
4. **Test**: Verify with integration tests that batch boundaries are handled correctly
5. **Monitor**: Track progress to ensure no notifications are skipped
