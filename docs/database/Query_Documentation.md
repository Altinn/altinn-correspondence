# Query Documentation
## Dialog Activity Export Queries

---

## Query 1: Issue #1951 - Migrated Events (NOT Synced from Altinn2)

### Purpose
Export dialog activities for correspondences that were migrated to the new system but were **NOT synced** from Altinn2. These represent local status changes made after migration.

### Business Context
- **Issue:** #1951 - Missing DialogPorten activities for migrated correspondences
- **Affected Records:** ~150 million status records
- **Fix Date:** May 19, 2026 11:35:59
- **Oldest Correspondence:** March 23, 2019

### Query Logic

```sql
-- Select Status 4 (CorrespondenceOpened) events
SELECT 
    er."ReferenceValue" AS DialogId,              -- External Dialog reference
    idcFetch."Id" AS "DialogActivityId",          -- Idempotency key for this activity
    stats."CorrespondenceId",                     -- Link to correspondence
    stats."StatusChanged" AS Timestamp,           -- When action occurred
    ap."OutputActorId" AS ActorId,                -- Who performed the action (output format)
    ap."Name" AS ActorName,                       -- Actor's display name
    4 AS "Status",                                -- Status code (4 = Fetched/Opened)
    'CorrespondenceOpened' AS ActivityType        -- Activity type label

FROM correspondence."CorrespondenceStatuses" stats

-- Join 1: Get correspondence details
INNER JOIN correspondence."Correspondences" corr 
    ON stats."CorrespondenceId" = corr."Id" 
    AND corr."Altinn2CorrespondenceId" IS NOT NULL   -- Only migrated correspondences
    AND corr."IsMigrating" = false                   -- Migration completed
    AND stats."SyncedFromAltinn2" IS NULL            -- ⭐ NOT synced from A2

-- Join 2: Get actor information from A2 party data
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
    AND corr."Recipient" <> ap."RecipientUrn"        -- Exclude actions by recipient

-- Join 3: Get external reference (Dialog ID)
INNER JOIN correspondence."ExternalReferences" er 
    ON stats."CorrespondenceId" = er."CorrespondenceId" 
    AND er."ReferenceType" = 3                       -- Type 3 = DialogPorten reference

-- Join 4: Get idempotency key for Status 4 (Fetched)
LEFT JOIN correspondence."IdempotencyKeys" idcFetch 
    ON stats."CorrespondenceId" = idcFetch."CorrespondenceId" 
    AND idcFetch."StatusAction" = '3'                -- StatusAction 3 = Fetched

WHERE 
    stats."Status" = 4                               -- Only Status 4 (Opened)
    AND stats."StatusChanged" < '2026-05-19 11:35:59'  -- Before fix deployment
    AND corr."Created" > '2019-03-23'                  -- After system availability

UNION ALL

-- Select Status 6 (CorrespondenceConfirmed) events
-- (Same structure as above, but for Status 6)
SELECT 
    er."ReferenceValue" AS DialogId,
    idcConfirm."Id" AS "DialogActivityId",
    stats."CorrespondenceId",
    stats."StatusChanged" AS Timestamp,
    ap."OutputActorId" AS ActorId,
    ap."Name" AS ActorName,
    6 AS "Status",
    'CorrespondenceConfirmed' AS ActivityType

FROM correspondence."CorrespondenceStatuses" stats
INNER JOIN correspondence."Correspondences" corr 
    ON stats."CorrespondenceId" = corr."Id" 
    AND corr."Altinn2CorrespondenceId" IS NOT NULL
    AND corr."IsMigrating" = false
    AND stats."SyncedFromAltinn2" IS NULL            -- ⭐ NOT synced from A2

INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
    AND corr."Recipient" <> ap."RecipientUrn"        -- Exclude actions by recipient

INNER JOIN correspondence."ExternalReferences" er 
    ON stats."CorrespondenceId" = er."CorrespondenceId" 
    AND er."ReferenceType" = 3

LEFT JOIN correspondence."IdempotencyKeys" idcConfirm 
    ON stats."CorrespondenceId" = idcConfirm."CorrespondenceId" 
    AND idcConfirm."StatusAction" = '6'              -- StatusAction 6 = Confirmed

WHERE 
    stats."Status" = 6                               -- Only Status 6 (Confirmed)
    AND stats."StatusChanged" < '2026-05-19 11:35:59'
    AND corr."Created" > '2019-03-23'

ORDER BY "CorrespondenceId";  -- For cursor-based pagination
```

### Key Filters Explained

| Filter | Purpose | Impact |
|--------|---------|--------|
| `stats."Status" IN (4, 6)` | Only Opened and Confirmed events | Reduces from 975M to ~569M rows |
| `stats."SyncedFromAltinn2" IS NULL` | Only migrated (not synced) events | Reduces to ~150M rows |
| `stats."StatusChanged" < '2026-05-19'` | Before fix deployment | Excludes new data |
| `corr."Altinn2CorrespondenceId" IS NOT NULL` | Only migrated correspondences | Excludes A3-only data |
| `corr."IsMigrating" = false` | Migration completed | Excludes in-progress migrations |
| `corr."Created" > '2019-03-23'` | Valid date range | Business requirement |
| `corr."Recipient" <> ap."RecipientUrn"` | Exclude recipient's own actions | Only delegated actions |

### A2Parties Columns Explained

The `A2Parties` table contains two key columns:

| Column | Purpose | Format |
|--------|---------|--------|
| `OutputActorId` | Export output for DialogPorten | Varies by user type (see below) |
| `RecipientUrn` | For comparing with `Correspondences.Recipient` | Varies by user type (see below) |

**Three User Type Formats:**

| User Type | Recipient Format | OutputActorId Format | RecipientUrn Format | Match? |
|-----------|------------------|----------------------|---------------------|--------|
| **Self-identified** | `urn:altinn:party:uuid:f48a5e8b-...` | `urn:altinn:person:legacy-selfidentified:MurgitroydFinland` | `urn:altinn:party:uuid:f48a5e8b-...` | ✅ UUID |
| **Person (SSN)** | `urn:altinn:person:identifier-no:10078328644` | `urn:altinn:person:identifier-no:10078328644` | `urn:altinn:person:identifier-no:10078328644` | ✅ Direct |
| **Organization** | `urn:altinn:organization:identifier-no:983415113` | `urn:altinn:organization:identifier-no:983415113` | `urn:altinn:organization:identifier-no:983415113` | ✅ Direct |

**Why RecipientUrn is needed:**
- Self-identified users have different formats in `Recipient` (UUID) vs `OutputActorId` (legacy name)
- Without conversion, filter `Recipient <> OutputActorId` would never match for self-identified users
- `RecipientUrn` converts to UUID format for self-identified, enabling correct comparison
- For Person and Organization types, `RecipientUrn` equals `OutputActorId` (already matching formats)

**Column Semantics:**
- `OutputActorId`: What gets exported to DialogPorten (the correct external format)
- `RecipientUrn`: For internal filtering logic (matches Correspondences.Recipient format)

### Current Performance Problem

```text
EXPLAIN ANALYZE output showed:
- Parallel Seq Scan on "CorrespondenceStatuses"
- Rows scanned: 975,517,862 (entire table!)
- Rows filtered out: 690,767,446
- Execution time: 2,035,861 ms (33.9 minutes)
- I/O time: 3,748,504 ms (62.5 minutes on disk)
```

**Problem:** No suitable index exists, causing full table scan of 975M rows.

---

## Query 2: Issue #1716 - Synced Events (From Altinn2)

### Purpose
Export dialog activities for correspondences that were **synced from Altinn2**. These represent historical events brought over during migration.

### Business Context
- **Issue:** #1716 - Missing DialogPorten activities for synced Altinn2 events
- **Affected Records:** ~7-9 million status records
- **Fix Date:** February 15, 2026
- **No correspondence date filter** (uses sync timestamp instead)

### Key Differences from Issue #1951

| Aspect | Issue #1951 | Issue #1716 |
|--------|-------------|-------------|
| **Filter** | `SyncedFromAltinn2 IS NULL` | `SyncedFromAltinn2 IS NOT NULL` |
| **Timestamp** | `StatusChanged` column | `SyncedFromAltinn2` column |
| **Date Range** | `< '2026-05-19'` | `< '2026-02-15'` |
| **Created Filter** | `Created > '2019-03-23'` | None |
| **Record Count** | ~150 million | ~7-9 million |

### Expected Performance (With Indexes)

```text
With proper indexes:
- Index Scan on IX_CorrespondenceStatuses_Status_SyncedTimestamp_Synced
- Rows scanned: ~7-9 million (only matching rows)
- Estimated execution time: < 30 seconds
- Export time (with batching): 15-30 minutes
```

---

## Output Format

Both queries produce CSV with the following columns:

| Column | Type | Description |
|--------|------|-------------|
| DialogId | String | External DialogPorten reference (UUID) |
| DialogActivityId | UUID | Idempotency key (null if not yet created) |
| Timestamp | ISO 8601 | When the status change occurred |
| ActorId | String | Actor's identifier in URN format |
| ActorName | String | Actor's display name |
| ActivityType | String | "CorrespondenceOpened" or "CorrespondenceConfirmed" |

### Sample Output
```csv
DialogId,DialogActivityId,Timestamp,ActorId,ActorName,ActivityType
"a1b2c3d4-...",,"2025-03-15T10:30:00Z","urn:altinn:person:01019012345","John Doe","CorrespondenceOpened"
```
