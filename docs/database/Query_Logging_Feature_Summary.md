# Query Logging in Test Mode - Summary

## Date: 2024-06-02
## Feature: Added SQL query logging when test mode is active

---

## ✅ Changes Made

### 1. Added Test Mode Tracking

**Added field to DialogActivityExportService:**
```csharp
private bool _isTestMode; // Track test mode for query logging
```

**Set in ExportToCSVAsync and ExportBothToCSVAsync:**
```csharp
// Set test mode for query logging
_isTestMode = maxBatches.HasValue;
```

When `--max-batches` is specified, test mode is automatically enabled and queries will be logged.

---

### 2. Query Logging in FetchStatusRecordsAsync

**Added detailed query logging:**
```csharp
// Log query in test mode for verification
if (_isTestMode)
{
    var logQuery = query
        .Replace("@cutoffTimestamp", $"'{cutoffTimestamp:yyyy-MM-dd HH:mm:ss}'")
        .Replace("@lastId", lastCursor.HasValue ? $"'{lastCursor.Value.correspondenceId}'" : "NULL")
        .Replace("@lastStatus", lastCursor.HasValue ? lastCursor.Value.status.ToString() : "NULL")
        .Replace("@fetchLimit", _batchSize.ToString());

    _logger.LogInformation("TEST MODE - Executing query for Status {StatusValue}:", statusValue);
    _logger.LogInformation("{Query}", logQuery);
}
```

---

## 🎯 What Gets Logged

When running in test mode (`--max-batches` specified), you'll see:

### For Each Status Query (4 and 6):

```
info: TEST MODE - Executing query for Status 4:
info: WITH filtered AS (
         SELECT 
             stats."CorrespondenceId",
             stats."PartyUuid",
             stats."StatusChanged",
             stats."Status"
         FROM correspondence."CorrespondenceStatuses" stats
         WHERE stats."Status" = 4
           AND stats."StatusChanged" BETWEEN '2019-03-23 00:00:00' AND '2026-05-19 11:35:59'
           AND stats."SyncedFromAltinn2" IS NULL
         ORDER BY stats."CorrespondenceId", stats."Status"
         LIMIT 1000
     )
     SELECT 
         er."ReferenceValue" AS DialogId,
         idcFetch."Id" AS DialogActivityId,
         filtered."CorrespondenceId",
         filtered."StatusChanged" AS Timestamp,
         ap."OutputActorId" AS ActorId,
         ap."Name" AS ActorName,
         4 AS Status,
         'CorrespondenceOpened' AS ActivityType
     FROM filtered
     INNER JOIN correspondence."Correspondences" corr 
         ON filtered."CorrespondenceId" = corr."Id" 
         AND corr."Altinn2CorrespondenceId" IS NOT NULL 
         AND corr."IsMigrating" = FALSE
     INNER JOIN correspondence."A2Parties" ap 
         ON filtered."PartyUuid" = ap."PartyUuid"
         AND corr."Recipient" <> ap."RecipientUrn"
     INNER JOIN correspondence."ExternalReferences" er
         ON filtered."CorrespondenceId" = er."CorrespondenceId" 
         AND er."ReferenceType" = 3
     INNER JOIN correspondence."IdempotencyKeys" idcFetch
         ON filtered."CorrespondenceId" = idcFetch."CorrespondenceId" 
         AND idcFetch."StatusAction" = '3'
     ORDER BY filtered."CorrespondenceId", filtered."Status"
```

### Benefits:

1. ✅ **Verify Query Structure** - See exact SQL being executed
2. ✅ **Check Parameters** - All parameters are substituted with actual values
3. ✅ **Verify StatusAction Mapping** - Confirm Status 4 uses StatusAction = 3
4. ✅ **Copy-Paste Testing** - Can copy query directly to DBeaver/pgAdmin
5. ✅ **Debug Issues** - Troubleshoot query problems quickly

---

## 📋 Usage Example

### Run Test with Query Logging:

```powershell
cd tools\Altinn.Correspondence.DialogActivityExporter
.\test-export.ps1
```

### Console Output Will Include:

```
info: Starting export for Issue #1951
info: TEST MODE: Limited to 2 batch(es)
info: Skipping total count calculation (test mode)

info: TEST MODE - Executing query for Status 4:
info: SELECT er."ReferenceValue" AS DialogId, ...
      (full query with substituted parameters)

info: TEST MODE - Executing query for Status 6:
info: SELECT er."ReferenceValue" AS DialogId, ...
      (full query with substituted parameters)

info: Batch 1: 1,000 rows in 2,134ms

info: TEST MODE - Executing query for Status 4:
info: SELECT er."ReferenceValue" AS DialogId, ...
      (query with cursor values for pagination)

info: TEST MODE - Executing query for Status 6:
info: SELECT er."ReferenceValue" AS DialogId, ...
      (query with cursor values for pagination)

info: Batch 2: 1,000 rows in 2,087ms
info: Reached max batch limit (2). Stopping test export.
```

---

## 🔍 Verification Points

### What to Check in Logged Queries:

1. **StatusAction Mapping:**
   - Status 4: Should see `AND idcFetch."StatusAction" = '3'`
   - Status 6: Should see `AND idcConfirm."StatusAction" = '6'`

2. **Filter Conditions:**
   - Issue #1951: Should see `stats."SyncedFromAltinn2" IS NULL`
   - Issue #1716: Should see `stats."SyncedFromAltinn2" IS NOT NULL`

3. **Timestamp Column:**
   - Issue #1951: Should use `stats."StatusChanged"`
   - Issue #1716: Should use `stats."SyncedFromAltinn2"`

4. **Cursor Pagination (Batch 2+):**
   - Should see actual UUID and status values: `(stats."CorrespondenceId", stats."Status") > ('abc-123...', 4)`

5. **Join Aliases:**
   - Status 4: Uses `idcFetch` alias
   - Status 6: Uses `idcConfirm` alias

---

## 🚀 Testing Workflow

### 1. Run Test Mode:
```powershell
.\test-export.ps1 -MaxBatches 1
```

### 2. Review Logged Queries:
- Check StatusAction mapping (3 for Status 4, 6 for Status 6)
- Verify filter conditions match expected issue
- Confirm timestamp column is correct

### 3. Copy Query to DBeaver (Optional):
- Copy the logged query
- Paste into DBeaver/pgAdmin
- Run to verify results match expectations
- Check EXPLAIN ANALYZE to verify index usage

### 4. Verify Output:
- Open generated CSV file
- Confirm DialogActivityId is populated (not empty)
- Verify ActivityType matches Status (CorrespondenceOpened for Status 4)

---

## 💡 Production Mode

**Query logging is NOT active in production mode:**

When running without `--max-batches`:
```powershell
dotnet run -- --issue 1951 --output export.csv --azure-ad --cutoff "2026-05-19 11:35:59" --yes
```

No query logging will occur. This keeps production logs clean and focused on progress/errors only.

---

## ✅ Build Status

- ✅ Build successful
- ✅ Query logging implemented
- ✅ Only active in test mode
- ✅ Ready to test

---

## 📖 Related Documentation

- **Max_Batches_Feature_Summary.md** - Test mode feature details
- **Testing_Guide.md** - Complete testing guide
- **Test_Export_Query.sql** - Manual test queries for comparison

---

**You can now verify the exact SQL queries being executed in test mode!** 🎉
