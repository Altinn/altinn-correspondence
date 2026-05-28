# Database Documentation
## Dialog Activity Export - Index Optimization

This folder contains documentation for optimizing PostgreSQL queries used to export dialog activity data for Issues #1951 and #1716.

## Overview

We need to export ~150-160 million rows from the `CorrespondenceStatuses` table. Without proper indexes, queries take **1+ hour** due to full table scans. With the recommended indexes, queries complete in **minutes**.

**Scope:** Export operations only. Runtime API performance is already well-optimized.

## Documents

1. **[Quick_Reference.md](Quick_Reference.md)** - Deployment checklist, metrics, and FAQ
2. **[Index_Creation_Scripts.sql](Index_Creation_Scripts.sql)** - Production-ready SQL scripts for export indexes
3. **[Fix_A2Parties_Recipient_Filter.sql](Fix_A2Parties_Recipient_Filter.sql)** - A2Parties table setup (column rename + index)
4. **[Technical_Documentation.md](Technical_Documentation.md)** - Detailed analysis, query patterns, and business case
5. **[Query_Documentation.md](Query_Documentation.md)** - Query explanations and filter logic

## Quick Start

### Phase 1: Issue #1716 (15 minutes)
```sql
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_CorrespondenceStatuses_Status_SyncedTimestamp_Synced"
ON correspondence."CorrespondenceStatuses" ("Status", "SyncedFromAltinn2")
INCLUDE ("CorrespondenceId", "PartyUuid")
WHERE "SyncedFromAltinn2" IS NOT NULL;
```

### Phase 2: Issue #1951 (60 minutes)
```sql
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_CorrespondenceStatuses_Status_StatusChanged_Migrated"
ON correspondence."CorrespondenceStatuses" ("Status", "StatusChanged")
INCLUDE ("CorrespondenceId", "PartyUuid")
WHERE "SyncedFromAltinn2" IS NULL;
```

**Total:** 13.5 GB disk space, 90 minutes deployment time

**Note:** A2Parties table setup is handled separately (see below)

## Key Metrics

| Metric | Before | After | Improvement |
|--------|--------|-------|-------------|
| Query Time | 33+ minutes | < 5 seconds | **400x faster** |
| Export Time | 4-6 hours | 30-60 minutes | **5-10x faster** |
| Disk I/O | 30.7M buffers | < 100K buffers | **99% reduction** |

## Safety

- ✅ All indexes use `CONCURRENTLY` - zero downtime
- ✅ No table locks - production unaffected
- ✅ Fully reversible - can drop indexes if needed
- ✅ No impact on runtime API performance

## Issues Reference

- **Issue #1951:** ~150 million migrated events (`SyncedFromAltinn2 IS NULL`)
- **Issue #1716:** ~7-9 million synced events (`SyncedFromAltinn2 IS NOT NULL`)

## Related Files

```
altinn-correspondence/
├── docs/database/
│   ├── README.md (this file)
│   ├── Quick_Reference.md
│   ├── Index_Creation_Scripts.sql
│   ├── Technical_Documentation.md
│   └── Query_Documentation.md
└── tools/Altinn.Correspondence.DialogActivityExporter/
    └── README.md (export tool usage)
```
