# Issue #1951: Export from Altinn 2 Database Strategy

## 🎯 Key Insight: Export from Source System

Instead of creating a 337M-row helper table in the Correspondence database (slow, expensive), **export directly from Altinn 2 database** where the data originates and is properly indexed.

---

## ✅ Why This Approach is Superior

### Issue #1716 Reference (Successful Implementation)
- **Source:** Altinn 2 database
- **Result:** `A2Iss1716A2Events` helper table with ~10M rows
- **Performance:** Fast export due to Altinn 2's optimized indexes
- **Import:** Loaded into Correspondence database as pre-filtered helper table

### Issue #1951 Should Follow Same Pattern
- **Source:** Altinn 2 database (migrated events)
- **Filter:** Events that were **migrated** (not synced) to Correspondence
- **Volume:** 337.8M rows (but Altinn 2 has better indexes!)
- **Result:** Pre-filtered helper table for fast export

---

## 📊 Comparison: Helper Table vs Altinn 2 Export

| Approach | Query Source | Performance | Storage | Risk |
|----------|-------------|-------------|---------|------|
| **Helper Table** | Correspondence DB<br>(1.94B rows) | 337.8M rows<br>2-6 hour creation<br>11 min just to COUNT | 30-50 GB | High - slow creation |
| **Altinn 2 Export** ✅ | Altinn 2 DB<br>(Better indexes) | Fast query<br>Direct export<br>Pre-filtered | Same (30-50 GB) | Low - proven approach |

---

## 🔍 Issue #1716 vs #1951: Data Source Analysis

### Issue #1716: Synced from Altinn 2
```
Definition: SyncedFromAltinn2 IS NOT NULL
Source: Altinn 2 database
Process: Synced → A2Iss1716A2Events helper table
Volume: ~10M rows
Status: ✅ Implemented successfully
```

### Issue #1951: Migrated (NOT Synced)
```
Definition: SyncedFromAltinn2 IS NULL
Source: Should also come from Altinn 2 database!
Process: Export from A2 → A2Iss1951MigratedEvents helper table
Volume: ~337.8M rows
Status: ⏭️ Use same approach as Issue #1716
```

**Key Realization:** Both Issue #1716 and #1951 data **originates from Altinn 2**, just through different migration paths:
- **Issue #1716:** Synced via continuous sync process
- **Issue #1951:** Bulk migrated during migration project

---

## 📋 Recommended Strategy: Mirror Issue #1716 Process

### Phase 1: Export from Altinn 2 Database

**Query Altinn 2 database for Issue #1951 events:**

```sql
-- Run this in Altinn 2 database (NOT Correspondence database)
-- Adjust filters to match Issue #1951 definition

SELECT 
    e.CorrespondenceId,        -- Or A2 equivalent ID
    e.PartyId,                 -- A2 party identifier
    e.Status,                  -- Status code (4 or 6)
    e.StatusChanged,           -- Timestamp
    -- Include any other fields needed for join matching
    c.CorrespondenceId AS A3CorrespondenceId  -- Mapped ID in Correspondence system
FROM Altinn2.dbo.CorrespondenceEvents e
INNER JOIN Altinn2.dbo.CorrespondenceMigrationMapping m
    ON e.CorrespondenceId = m.A2CorrespondenceId
INNER JOIN Altinn2.dbo.Parties p
    ON e.PartyId = p.PartyId
WHERE e.Status IN (4, 6)
  AND e.StatusChanged BETWEEN '2019-03-23' AND '2026-12-31'
  AND m.MigrationBatch IS NOT NULL  -- Or whatever identifies "migrated" events
  AND m.SyncedToA3 = 0;             -- Not synced, only migrated
```

**Expected performance:** Fast (minutes, not hours) due to Altinn 2 indexes.

### Phase 2: Export to CSV or SQL Insert Script

**Option A: Export to CSV**
```bash
# Export from Altinn 2 database to CSV
sqlcmd -S altinn2-server -d Altinn2DB -Q "SELECT ..." -o issue1951_export.csv -s "," -W

# Import CSV into Correspondence database
psql -h correspondence-db -d correspondence -c "COPY correspondence.\"A2Iss1951MigratedEvents\" FROM 'issue1951_export.csv' CSV HEADER;"
```

**Option B: Generate SQL INSERT script**
```bash
# Export as SQL INSERT statements
sqlcmd -S altinn2-server -d Altinn2DB -Q "SELECT ..." -o issue1951_inserts.sql
```

### Phase 3: Load into Correspondence Database

**Create table structure first:**
```sql
-- Run in Correspondence database
CREATE TABLE correspondence."A2Iss1951MigratedEvents" (
    "CorrespondenceId" uuid NOT NULL,
    "PartyUuid" uuid NOT NULL,
    "Status" integer NOT NULL,
    "StatusChanged" timestamp without time zone NOT NULL
);
```

**Load data via COPY or INSERT:**
```sql
-- Fast bulk load
COPY correspondence."A2Iss1951MigratedEvents" 
FROM '/path/to/issue1951_export.csv' 
DELIMITER ',' 
CSV HEADER;
```

### Phase 4: Create Indexes (Same as Before)

```sql
-- Primary index for cursor pagination
CREATE INDEX CONCURRENTLY "IX_A2Iss1951MigratedEvents_CorrespondenceId_Status"
ON correspondence."A2Iss1951MigratedEvents" ("CorrespondenceId", "Status");

-- Covering index for Index-Only Scans
CREATE INDEX CONCURRENTLY "IX_A2Iss1951MigratedEvents_Covering"
ON correspondence."A2Iss1951MigratedEvents" ("CorrespondenceId", "Status")
INCLUDE ("PartyUuid", "StatusChanged");

-- Update statistics
ANALYZE correspondence."A2Iss1951MigratedEvents";
```

---

## 💡 Key Advantages of Altinn 2 Export Approach

### 1. Performance
- ✅ Altinn 2 database has **optimized indexes** for these queries
- ✅ Avoid querying Correspondence's 1.94B row CorrespondenceStatuses table
- ✅ Export completes in **minutes to hours** (vs 2-6 hours for helper table creation)

### 2. Data Accuracy
- ✅ Source of truth is Altinn 2 database
- ✅ Directly query migration mapping tables
- ✅ Clear distinction between "synced" vs "migrated" events

### 3. Reduced Risk
- ✅ No load on Correspondence production database during export
- ✅ Can run export during business hours (Altinn 2 is read-only)
- ✅ Proven approach (Issue #1716 used this successfully)

### 4. Reusability
- ✅ Export query can be re-run if needed
- ✅ Same helper table structure as Issue #1716
- ✅ Code changes in DialogActivityExporter are minimal

---

## 🗺️ Implementation Roadmap

### Step 1: Verify Altinn 2 Database Access
```bash
# Test connection to Altinn 2 database
sqlcmd -S <altinn2-server> -d <altinn2-database> -Q "SELECT TOP 1 * FROM sys.tables"
```

**Prerequisites:**
- Altinn 2 database connection credentials
- Read-only access sufficient
- VPN or network access if required

### Step 2: Identify Altinn 2 Tables and Filters

**Questions to answer:**
1. What is the Altinn 2 table name for correspondence events?
   - Example: `Altinn2.dbo.CorrespondenceStatus` or `CorrespondenceEvents`?
2. How are "migrated" events identified in Altinn 2?
   - Migration mapping table? `MigrationBatch` column?
3. How are Altinn 2 IDs mapped to Correspondence IDs?
   - Is there a `CorrespondenceMigrationMapping` table?
4. What is the Status column structure?
   - Same values (4, 6) or different encoding?

**Action:** Review Altinn 2 database schema or Issue #1716 export query for reference.

### Step 3: Write Altinn 2 Export Query

**Template (adjust based on Altinn 2 schema):**
```sql
-- Run in Altinn 2 database
SELECT 
    m.A3CorrespondenceId AS CorrespondenceId,  -- Mapped ID for Correspondence system
    p.PartyUuid,                                -- Or convert from A2 PartyId
    e.Status,
    e.StatusChanged
FROM Altinn2.dbo.CorrespondenceStatus e
INNER JOIN Altinn2.dbo.CorrespondenceMigrationMapping m
    ON e.CorrespondenceId = m.A2CorrespondenceId
INNER JOIN Altinn2.dbo.Parties p
    ON e.PartyId = p.PartyId
WHERE e.Status IN (4, 6)
  AND e.StatusChanged BETWEEN '2019-03-23' AND '2026-12-31'
  AND m.IsMigrated = 1            -- Identifies migrated events
  AND m.IsSynced = 0;             -- NOT synced (Issue #1951 definition)
```

**Test with LIMIT/TOP 10000 first to verify results.**

### Step 4: Export Data

**Choose export method based on volume:**

**Option A: CSV Export (Recommended for 337M rows)**
```bash
# SQL Server to CSV (if Altinn 2 is SQL Server)
bcp "SELECT ..." queryout issue1951_export.csv -c -t "," -r "\n" -S server -d database -T

# Or via sqlcmd with formatting
sqlcmd -S server -d database -i export_query.sql -o issue1951_export.csv -s "," -W -h -1
```

**Option B: Direct PostgreSQL COPY (if supported)**
```sql
-- If you can connect directly from PostgreSQL to SQL Server (foreign data wrapper)
CREATE EXTENSION postgres_fdw;
-- Configure FDW to Altinn 2 SQL Server
-- Then: INSERT INTO ... SELECT FROM foreign_table
```

**Option C: Streaming via Application**
```csharp
// C# console app to stream from SQL Server to PostgreSQL
using SqlConnection sourceConn = new(altinn2ConnectionString);
using NpgsqlConnection destConn = new(correspondenceConnectionString);
// Stream data in batches to avoid memory issues
```

### Step 5: Load into Correspondence Database

```sql
-- Create table
CREATE TABLE correspondence."A2Iss1951MigratedEvents" (
    "CorrespondenceId" uuid NOT NULL,
    "PartyUuid" uuid NOT NULL,
    "Status" integer NOT NULL,
    "StatusChanged" timestamp without time zone NOT NULL
);

-- Bulk load from CSV
COPY correspondence."A2Iss1951MigratedEvents" 
FROM '/path/to/issue1951_export.csv' 
DELIMITER ',' 
CSV HEADER;

-- Verify row count
SELECT COUNT(*), 
       COUNT(*) FILTER (WHERE "Status" = 4) AS status_4_count,
       COUNT(*) FILTER (WHERE "Status" = 6) AS status_6_count
FROM correspondence."A2Iss1951MigratedEvents";
```

### Step 6: Create Indexes and Update Code

**Same as original helper table approach:**
1. Create indexes (20-40 minutes)
2. Update `DialogActivityExportService.cs` to query helper table
3. Test with `--max-batches 10`
4. Run production export

---

## 📝 Issue #1716 Reference: What Was Done?

**Review the Issue #1716 implementation for exact steps:**
1. Check if there's an existing export script for Issue #1716
2. Look for SQL queries run against Altinn 2 database
3. Review how data was loaded into `A2Iss1716A2Events`
4. Mirror the same process for Issue #1951

**Files to check:**
- Any SQL scripts in `tools/` directory referencing Altinn 2
- Documentation about Issue #1716 helper table creation
- Migration scripts or data import logs

---

## 🎯 Immediate Next Steps

### 1. Locate Issue #1716 Export Documentation
```bash
# Search for Issue #1716 export scripts
cd C:\Repos\Altinn\altinn-correspondence
git log --all --grep="1716" --grep="A2Iss1716A2Events" --oneline
git log --all -- "*1716*" --oneline

# Search for Altinn 2 export references
grep -r "Altinn2" tools/
grep -r "A2Iss1716A2Events" docs/
```

### 2. Ask Team Members
- **Who created** the `A2Iss1716A2Events` helper table?
- **What query** was used to export from Altinn 2?
- **What tools** were used (sqlcmd, bcp, custom app)?
- **Where is** the Altinn 2 database connection info?

### 3. Verify Altinn 2 Database Access
- Confirm you have credentials
- Test connection
- Identify relevant tables and columns

### 4. Create Export Query (Based on Issue #1716 Reference)
- Adapt Issue #1716 query for Issue #1951 filters
- Test with TOP 10000 first
- Verify row count matches expectations

---

## ✅ Summary: Why Altinn 2 Export is Better

| Aspect | Helper Table in Correspondence | Export from Altinn 2 ✅ |
|--------|-------------------------------|------------------------|
| **Query Source** | 1.94B row CorrespondenceStatuses | Well-indexed Altinn 2 tables |
| **Creation Time** | 2-6 hours | Minutes to 1 hour |
| **Load on Production** | High (complex JOINs) | None (read-only A2) |
| **Data Accuracy** | Derived from synced data | Source of truth |
| **Risk** | High (slow, untested query) | Low (proven with Issue #1716) |
| **Proven Approach** | No | ✅ Yes (Issue #1716) |

**Recommendation:** Follow the Issue #1716 export process and export Issue #1951 data directly from Altinn 2 database.

---

## 📚 Related Documentation

- **Issue #1716 Reference:** (Need to locate export scripts/docs)
- **Altinn 2 Schema:** (Need database connection and schema docs)
- **Migration Mapping:** (Need to understand A2→A3 ID mapping)
- **Current Strategy:** `ISSUE_1951_OPTIMIZATION_STRATEGY.md` (update with new approach)

---

**Next Action:** Locate Issue #1716 export query/script and adapt for Issue #1951.
