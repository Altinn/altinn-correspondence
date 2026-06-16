# Helper Table Schema: Source Column Documentation

## 📊 Helper Table Columns

### A2Iss1716A2Events (Synced Events)
```sql
CREATE TABLE correspondence."A2Iss1716A2Events" (
    "CorrespondenceId" uuid NOT NULL,
    "Timestamp" timestamptz NOT NULL,
    "PartyUuid" uuid NOT NULL,
    "Status" int4 NOT NULL,
    "Source" int4 NOT NULL  -- NEW: 0=ServiceEngine, 1=Archive
);
```

### A2Iss1951MigratedEvents (Migrated Events)
```sql
CREATE TABLE correspondence."A2Iss1951MigratedEvents" (
    "CorrespondenceId" uuid NOT NULL,
    "PartyUuid" uuid NOT NULL,
    "Status" int4 NOT NULL,
    "StatusChanged" timestamptz NOT NULL,
    "Source" int4 NOT NULL  -- NEW: 0=ServiceEngine, 1=Archive
);
```

---

## 🔍 Source Column Purpose

### Values
- **0** = ServiceEngine (Altinn 2 service engine events)
- **1** = Archive (Altinn 2 archive events)

### Purpose
- ✅ **Troubleshooting** - Identify which Altinn 2 source table an event came from
- ✅ **Debugging** - Track down data issues in source system
- ✅ **Human readability** - Makes manual queries easier to understand

### NOT Used For
- ❌ **Export queries** - Not filtered or joined on
- ❌ **Index optimization** - Not included in indexes
- ❌ **Performance** - Has no impact on query performance

---

## 📝 Usage Examples

### Troubleshooting Query
```sql
-- Find events from specific source
SELECT 
    "CorrespondenceId",
    "Status",
    "StatusChanged",
    CASE "Source" 
        WHEN 0 THEN 'ServiceEngine'
        WHEN 1 THEN 'Archive'
        ELSE 'Unknown'
    END AS source_system,
    COUNT(*) OVER (PARTITION BY "Source") AS events_from_source
FROM correspondence."A2Iss1951MigratedEvents"
WHERE "CorrespondenceId" = '12345678-1234-1234-1234-123456789012'
ORDER BY "StatusChanged";
```

### Data Quality Check
```sql
-- Distribution of events by source
SELECT 
    CASE "Source" 
        WHEN 0 THEN 'ServiceEngine'
        WHEN 1 THEN 'Archive'
        ELSE 'Unknown'
    END AS source_system,
    "Status",
    COUNT(*) AS event_count,
    MIN("StatusChanged") AS earliest_event,
    MAX("StatusChanged") AS latest_event
FROM correspondence."A2Iss1951MigratedEvents"
GROUP BY "Source", "Status"
ORDER BY "Source", "Status";
```

### Find Duplicate Sources
```sql
-- Check if same event exists in both sources
SELECT 
    "CorrespondenceId",
    "Status",
    "PartyUuid",
    "StatusChanged",
    COUNT(DISTINCT "Source") AS source_count,
    ARRAY_AGG(DISTINCT "Source" ORDER BY "Source") AS sources
FROM correspondence."A2Iss1951MigratedEvents"
GROUP BY "CorrespondenceId", "Status", "PartyUuid", "StatusChanged"
HAVING COUNT(DISTINCT "Source") > 1
LIMIT 100;
```

---

## 🚫 Why Source is NOT in Indexes

### Storage Efficiency
Including Source in indexes would:
- ❌ Increase index size by ~10-15%
- ❌ Slower index maintenance (INSERT/UPDATE)
- ❌ No query performance benefit (never filtered on)

### Query Performance
Export queries don't use Source:
```sql
-- Export query does NOT filter or join on Source
SELECT DISTINCT ...
FROM correspondence."A2Iss1951MigratedEvents" migEvents
INNER JOIN correspondence."CorrespondenceStatuses" stats 
    ON migEvents."CorrespondenceId" = stats."CorrespondenceId" 
    AND migEvents."Status" = stats."Status" 
    AND migEvents."PartyUuid" = stats."PartyUuid"
    AND migEvents."StatusChanged" = stats."StatusChanged"
WHERE migEvents."Status" = 4  -- No filter on Source!
```

### Index Design
**Optimal indexes exclude Source:**
```sql
-- ✅ GOOD: Source NOT included
CREATE INDEX "IX_A2Iss1951MigratedEvents_Covering"
ON correspondence."A2Iss1951MigratedEvents" ("CorrespondenceId", "Status")
INCLUDE ("PartyUuid", "StatusChanged");

-- ❌ BAD: Including Source adds no value
CREATE INDEX "IX_A2Iss1951MigratedEvents_Covering_WithSource"
ON correspondence."A2Iss1951MigratedEvents" ("CorrespondenceId", "Status")
INCLUDE ("PartyUuid", "StatusChanged", "Source");  -- Wastes space!
```

---

## 📊 Storage Impact

### Table Size Estimate
For 337.8M rows in A2Iss1951MigratedEvents:

**Without Source column:**
- Row size: ~44 bytes (4 UUIDs + 1 INT + 1 TIMESTAMP)
- Table: ~20-30 GB

**With Source column:**
- Row size: ~48 bytes (+4 bytes for INT4)
- Table: ~22-33 GB
- **Overhead: ~2-3 GB** (9% increase)

**For troubleshooting value, 2-3 GB is acceptable.**

---

## 🎯 Best Practices

### When Populating Helper Table from Altinn 2

**Set Source value based on origin:**
```sql
-- From ServiceEngine table
INSERT INTO correspondence."A2Iss1951MigratedEvents"
SELECT 
    CorrespondenceId,
    PartyUuid,
    Status,
    StatusChanged,
    0 AS Source  -- ServiceEngine
FROM Altinn2.ServiceEngine.CorrespondenceEvents
WHERE ...;

-- From Archive table
INSERT INTO correspondence."A2Iss1951MigratedEvents"
SELECT 
    CorrespondenceId,
    PartyUuid,
    Status,
    StatusChanged,
    1 AS Source  -- Archive
FROM Altinn2.Archive.CorrespondenceEvents
WHERE ...;
```

### Verification After Import
```sql
-- Verify Source values are valid
SELECT 
    "Source",
    COUNT(*) AS event_count,
    COUNT(*) * 100.0 / SUM(COUNT(*)) OVER () AS percentage
FROM correspondence."A2Iss1951MigratedEvents"
GROUP BY "Source"
ORDER BY "Source";

-- Expected output:
-- Source | event_count | percentage
-- -------|-------------|------------
--   0    |   250M      |   74%      (ServiceEngine)
--   1    |    88M      |   26%      (Archive)
```

---

## 📝 Documentation Updates

When documenting helper tables, always mention:

1. **Schema includes Source column** (INT4)
2. **Values: 0=ServiceEngine, 1=Archive**
3. **Purpose: Troubleshooting only, not used in queries**
4. **Not included in indexes** (no performance benefit)
5. **Storage overhead: ~9%** (acceptable for troubleshooting value)

---

## ✅ Summary

| Aspect | Details |
|--------|---------|
| **Column Name** | `Source` |
| **Data Type** | `int4` (4 bytes) |
| **Values** | 0=ServiceEngine, 1=Archive |
| **Purpose** | Troubleshooting, debugging, human readability |
| **Used in Queries** | ❌ No (never filtered or joined) |
| **In Indexes** | ❌ No (no performance benefit) |
| **Storage Overhead** | ~9% (~2-3 GB for 337M rows) |
| **Should Include** | ✅ Yes (troubleshooting value worth storage cost) |

---

**Recommendation:** Include Source column in both A2Iss1716A2Events and A2Iss1951MigratedEvents for troubleshooting, but exclude from all indexes.
