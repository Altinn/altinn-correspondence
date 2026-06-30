# Quick Assessment: Batch Boundary Risk

## Run This First

Execute **Query 13** from `notification-cleanup-monitoring.sql`:

```sql
SELECT 
	COUNT(DISTINCT "NotificationSent") as timestamps_with_duplicates,
	SUM(duplicate_count) as total_notifications_affected,
	ROUND(100.0 * SUM(duplicate_count) / (SELECT COUNT(*) 
		FROM correspondence."CorrespondenceNotifications"
		WHERE "Altinn2NotificationId" IS NOT NULL 
		  AND "SyncedFromAltinn2" IS NOT NULL
		  AND "NotificationSent" < '2026-04-25'::timestamptz), 2) as percent_affected
FROM (
	SELECT 
		"NotificationSent",
		COUNT(*) as duplicate_count
	FROM correspondence."CorrespondenceNotifications"
	WHERE "Altinn2NotificationId" IS NOT NULL 
	  AND "SyncedFromAltinn2" IS NOT NULL
	  AND "NotificationSent" < '2026-04-25'::timestamptz
	GROUP BY "NotificationSent"
	HAVING COUNT(*) > 1
) as duplicates;
```

## Interpreting Results

### Scenario 1: No Risk ✅
```
timestamps_with_duplicates: 0
total_notifications_affected: NULL
percent_affected: NULL
```
**Action**: Deploy as-is. No batch boundary risk.

---

### Scenario 2: Negligible Risk ⚠️
```
timestamps_with_duplicates: < 100
total_notifications_affected: < 1000
percent_affected: < 0.1%
```
**Action**: 
- Deploy as-is with monitoring
- Add comment in code noting the small risk
- Plan for manual cleanup if any notifications are skipped

---

### Scenario 3: Moderate Risk ⚠️⚠️
```
timestamps_with_duplicates: 100-1000
total_notifications_affected: 1000-10000
percent_affected: 0.1% - 1%
```
**Action**: 
- **Recommended**: Implement Option 1 (composite cursor)
- Alternative: Deploy with close monitoring, plan for reprocessing

---

### Scenario 4: High Risk ❌
```
timestamps_with_duplicates: > 1000
total_notifications_affected: > 10000
percent_affected: > 1%
```
**Action**: 
- **Must implement** Option 1 (composite cursor) before deploying
- Run Query 14 to see worst cases

---

## If You Need More Details

Run these additional queries:

**Query 12**: See the specific duplicate timestamps
```sql
-- Shows top 50 timestamps with duplicates
```

**Query 14**: Find high-risk cases (10+ notifications with same timestamp)
```sql
-- These are most likely to span batch boundaries with typical batch sizes (100-1000)
```

---

## Decision Tree

```
Run Query 13
	│
	├─ percent_affected = NULL or 0%
	│  └─> ✅ Deploy as-is
	│
	├─ percent_affected < 0.1%
	│  └─> ⚠️ Deploy with monitoring (accept small risk)
	│
	├─ percent_affected 0.1% - 1%
	│  └─> ⚠️⚠️ Strongly recommend Option 1
	│
	└─ percent_affected > 1%
	   └─> ❌ Must implement Option 1 before deploy
```

---

## Notes

- **Date Filter**: All queries now filter to `NotificationSent < '2026-04-25'` (Altinn2 cutoff)
- **NULL Checks Removed**: Altinn2 synced notifications always have `NotificationSent` populated
- **No Index Required**: These diagnostic queries run without the cleanup index (may be slower but will complete)
