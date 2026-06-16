# Issue #1951: Recommended Action Plan Based on Issue #1716 Approach

## 🎯 Critical Question: How Was A2Iss1716A2Events Created?

**You mentioned:** "I did for issue 1716" - exported from Altinn 2 database

**To replicate for Issue #1951, we need:**

1. **The original export query/script** used for Issue #1716
2. **The Altinn 2 database connection details** 
3. **The data mapping** (Altinn 2 IDs → Correspondence IDs)

---

## 📋 Immediate Action Items

### Action 1: Locate Issue #1716 Export Documentation

**Check these locations:**

```bash
# 1. Search Git history for A2Iss1716A2Events creation
cd C:\Repos\Altinn\altinn-correspondence
git log --all --grep="A2Iss1716A2Events" --oneline
git log --all --grep="1716" --grep="export" --oneline

# 2. Search for SQL scripts outside this repository
# (May be in a separate admin/scripts repo?)

# 3. Check documentation or wiki
# - Confluence/SharePoint pages about Issue #1716
# - Runbook or migration documentation
# - Email threads or Slack/Teams discussions

# 4. Ask team members who worked on Issue #1716
# - Who created the A2Iss1716A2Events table?
# - What query was used?
# - Where is the Altinn 2 connection string?
```

**What we need to find:**
- ✅ SQL query run against **Altinn 2 database** (not Correspondence database)
- ✅ How Altinn 2 IDs were mapped to Correspondence UUIDs
- ✅ Any filtering criteria for "synced" vs "migrated" events
- ✅ Data export method (CSV, SQL INSERT, direct INSERT INTO ... SELECT)

---

### Action 2: Examine A2Iss1716A2Events Table Structure

**Run this in Correspondence database to understand the table:**

```sql
-- Get table structure
\d+ correspondence."A2Iss1716A2Events"

-- Or via query:
SELECT 
    column_name,
    data_type,
    is_nullable
FROM information_schema.columns
WHERE table_schema = 'correspondence'
  AND table_name = 'A2Iss1716A2Events'
ORDER BY ordinal_position;

-- Sample some rows to see the data
SELECT * 
FROM correspondence."A2Iss1716A2Events"
LIMIT 10;

-- Check row count
SELECT 
    COUNT(*) AS total_rows,
    COUNT(*) FILTER (WHERE "Status" = 4) AS status_4_count,
    COUNT(*) FILTER (WHERE "Status" = 6) AS status_6_count,
    pg_size_pretty(pg_total_relation_size('correspondence."A2Iss1716A2Events"')) AS table_size
FROM correspondence."A2Iss1716A2Events";
```

**Expected columns (based on code analysis):**
- `CorrespondenceId` (UUID)
- `PartyUuid` (UUID)
- `Status` (INTEGER - 4 or 6)
- `StatusChanged` (TIMESTAMP)

---

### Action 3: Verify A2Iss1716A2Events Creation Method

**Hypothesis: Table was populated from Altinn 2 export**

**Test this by checking:**

```sql
-- 1. Check if table has any foreign keys or triggers
SELECT 
    conname AS constraint_name,
    contype AS constraint_type,
    pg_get_constraintdef(oid) AS constraint_definition
FROM pg_constraint
WHERE conrelid = 'correspondence."A2Iss1716A2Events"'::regclass;

-- 2. Check table creation time (may be in logs)
SELECT 
    schemaname,
    tablename,
    tableowner,
    tablespace
FROM pg_tables
WHERE tablename = 'A2Iss1716A2Events';

-- 3. Check if there are any comments on the table
SELECT 
    obj_description('correspondence."A2Iss1716A2Events"'::regclass) AS table_comment;
```

---

## 🔄 Two Possible Scenarios

### Scenario A: A2Iss1716A2Events Was Imported from Altinn 2 (Most Likely)

**If true:**
1. Find the export query that was run against Altinn 2 database
2. Adapt it for Issue #1951 (change filter from "synced" to "migrated")
3. Export from Altinn 2 → CSV → Import to Correspondence database
4. Create indexes
5. Update DialogActivityExportService.cs

**Timeline:** 2-4 hours (mostly export/import time)
**Risk:** Low (proven approach)

---

### Scenario B: A2Iss1716A2Events Was Created In-Place (Less Likely)

**If true:**
1. There was a CREATE TABLE AS SELECT query run in Correspondence database
2. It used complex JOINs similar to what we see in calculate-counts.sql
3. It took hours to complete (like what we're facing with Issue #1951)

**If this is the case:**
- We're back to the helper table creation problem
- But we can optimize by using Issue #1716's indexes as a template
- May need to create incrementally (by date ranges)

---

## 💡 Most Likely: Export from Altinn 2

**Evidence suggesting this approach:**

1. **Table name:** `A2Iss1716A2Events` 
   - "A2" prefix suggests Altinn 2 source
   - "Events" suggests event-based data from Altinn 2

2. **Your statement:** "I did for issue 1716"
   - Implies you personally ran the export
   - Suggests a known, repeatable process

3. **Performance:** 
   - Issue #1716 export is fast (~100ms queries)
   - This would be impossible if table was created slowly in Correspondence DB

4. **Data structure:**
   - Table has minimal columns (CorrespondenceId, PartyUuid, Status, StatusChanged)
   - Looks like a pre-filtered extract, not a full JOIN result

---

## 📝 Template for Issue #1951 (Once We Find Issue #1716 Query)

**Assuming Altinn 2 export approach, here's the likely template:**

```sql
-- Run in Altinn 2 database (SQL Server or whatever A2 uses)

-- Issue #1716: Synced events (SyncedFromAltinn2 IS NOT NULL)
SELECT 
    c.NewCorrespondenceId AS CorrespondenceId,  -- Mapped UUID
    p.PartyUuid,
    e.StatusCode AS Status,
    e.StatusChanged
FROM Altinn2.dbo.CorrespondenceStatus e
INNER JOIN Altinn2.dbo.CorrespondenceMapping c
    ON e.CorrespondenceId = c.OldCorrespondenceId
INNER JOIN Altinn2.dbo.PartyMapping p
    ON e.PartyId = p.OldPartyId
WHERE e.StatusCode IN (4, 6)
  AND e.StatusChanged < '2026-12-31'
  AND c.IsSynced = 1;  -- Issue #1716: SYNCED events

-- Issue #1951: Migrated events (SyncedFromAltinn2 IS NULL)
-- Same query, just change the last filter:
--   AND c.IsSynced = 0;  -- Issue #1951: NOT synced, only migrated
```

---

## 🚀 Next Steps (In Priority Order)

### Priority 1: Find Issue #1716 Export Query (CRITICAL)

**Action:** Search for the original export script/query

**Who to ask:**
- ✅ **You** (since you mentioned you did it for Issue #1716)
- ✅ Team members who worked on Issue #1716
- ✅ Database administrators who have Altinn 2 access
- ✅ Check migration project documentation

**Where to look:**
- Git repositories (this repo or others)
- Runbooks / SOPs
- Email or Teams/Slack conversations
- Database admin scripts folder
- Your own notes/scripts from when you did Issue #1716

### Priority 2: Document the Altinn 2 Export Process

Once found, document:
1. Altinn 2 database connection string (sanitize secrets)
2. Export query (exact SQL)
3. Export method (bcp, sqlcmd, custom tool)
4. Import method (COPY, INSERT, pg_restore)
5. Index creation scripts

### Priority 3: Adapt for Issue #1951

1. Change filter criteria (synced → migrated)
2. Verify expected row count (337.8M matches your earlier count)
3. Test export on small sample (TOP 10000)
4. Run full export (estimated time: 1-3 hours from Altinn 2)

### Priority 4: Load and Verify

1. Create `A2Iss1951MigratedEvents` table
2. Bulk load data
3. Create indexes
4. Verify row counts match
5. Test query performance (EXPLAIN ANALYZE)

### Priority 5: Update Code and Test

1. Add Issue #1951 branch in DialogActivityExportService.cs
2. Query `A2Iss1951MigratedEvents` (same pattern as Issue #1716)
3. Test with `--max-batches 10`
4. Run production export

---

## ⏱️ Estimated Timeline (If We Find Issue #1716 Script)

| Phase | Duration | Notes |
|-------|----------|-------|
| Find Issue #1716 script | 15-30 min | Ask team, search files |
| Adapt query for Issue #1951 | 15 min | Change filter criteria |
| Test export (10K rows) | 5 min | Verify query works |
| Full export from Altinn 2 | 1-3 hours | 337.8M rows, depends on A2 DB performance |
| Import to Correspondence | 30-60 min | COPY from CSV |
| Create indexes | 20-40 min | CONCURRENTLY |
| Update code | 30 min | Add Issue #1951 query branch |
| Test export | 1-2 hours | `--max-batches 10` |
| **TOTAL** | **4-8 hours** | Much faster than 2-6 hours for in-place helper table creation |

---

## 🎯 Immediate Action: Locate Issue #1716 Script

**Can you:**
1. Check your own files/notes for the Issue #1716 export script?
2. Search your email for "1716" + "export" or "A2Iss1716A2Events"?
3. Ask the team member who originally worked on Issue #1716?

**Once we have the Issue #1716 approach, Issue #1951 will be straightforward to implement using the same method.**

---

## 📚 Files Updated with New Strategy

- ✅ `ISSUE_1951_ALTINN2_EXPORT_STRATEGY.md` - High-level strategy
- ✅ `ISSUE_1951_ACTION_PLAN.md` - This file (concrete action items)

**Status:** ⏳ Waiting for Issue #1716 export documentation/script
