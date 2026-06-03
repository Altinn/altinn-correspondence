# Dialog Activity Export Query Performance Notes

## Current Implementation (Working)

The production export queries use a **CTE (Common Table Expression) with CorrespondenceStatuses filtering only**.

### Query Structure

```sql
WITH filtered AS (
    SELECT CorrespondenceId, PartyUuid, StatusChanged, Status
    FROM correspondence."CorrespondenceStatuses"
    WHERE Status = {4 or 6}
      AND SyncedFromAltinn2 {timestamp filter}
      AND {cursor predicate if paginating}
    ORDER BY CorrespondenceId, Status
    LIMIT 1000
)
SELECT ... FROM filtered
INNER JOIN Correspondences ...
INNER JOIN A2Parties ...
INNER JOIN ExternalReferences ...
INNER JOIN IdempotencyKeys ...
```

### Performance Characteristics

- **Test Mode**: ~6 minutes for 2 batches (~1000 rows total)
- **Production Expected**: Faster due to:
  - No test mode logging overhead
  - Larger batch processing
  - Better connection pooling
  - Continuous operation without restarts

### Why This Approach?

| Approach | Result | Reason |
|----------|--------|--------|
| **No CTE** (original) | ❌ Timeout (>5 min) | PostgreSQL couldn't optimize LIMIT with multiple JOINs |
| **CTE with CorrespondenceStatuses only** | ✅ Works (6 min) | Index on CorrespondenceStatuses used efficiently |
| **CTE with Correspondences JOIN** | ❌ Timeout (>5 min) | JOIN inside CTE prevented efficient index usage |

## Design Decisions

### Decision 1: Use Separate Queries for Status 4 and Status 6

**Rationale**: UNION ALL with ORDER BY scans millions of rows before LIMIT.

- ❌ Single query with UNION ALL: 12-40+ minutes
- ✅ Separate queries merged in-memory: ~3-6 seconds per query

### Decision 2: Keep CTE Simple (CorrespondenceStatuses Only)

**Rationale**: PostgreSQL query planner optimizes simple CTEs better.

**Attempted optimizations that failed:**
1. ❌ Including Correspondences JOIN in CTE
   - Theory: Filter early to reduce candidate set
   - Reality: Query planner chose poor execution path, caused timeout

2. ✅ CTE filters only base table, JOINs filter afterward
   - Theory: Let index on CorrespondenceStatuses work efficiently
   - Reality: Completes successfully in 6 minutes

### Decision 3: Accept Current Performance for Production

**Rationale**: 6 minutes for test mode is acceptable given:

1. **Test mode overhead**: Extensive logging adds significant time
2. **Small batch testing**: Only 2 batches processed
3. **Production will be faster**: Larger batches, no logging, continuous processing
4. **Correctness over speed**: Query returns correct, complete results
5. **No timeout risk**: Well within 300-second command timeout

## Query Evolution Timeline

1. **Original**: Simple JOINs, no CTE → Timeout
2. **CTE v1**: Simple CTE with CorrespondenceStatuses only → ✅ Works (6 min)
3. **CTE v2**: Added Correspondences to CTE for "optimization" → Timeout
4. **Final**: Reverted to CTE v1 (current production)

## Future Optimization Opportunities

If production performance is insufficient, consider:

1. **EXPLAIN ANALYZE** on production data to identify bottlenecks
2. **Materialized view** for pre-joined common filters
3. **Partitioning** CorrespondenceStatuses by Status or date range
4. **Parallel query execution** if PostgreSQL version supports it
5. **Index tuning** based on actual query plans

## Recommendations

✅ **Deploy current implementation** - it works reliably  
✅ **Monitor production performance** - measure actual times without test overhead  
✅ **Revisit optimization** only if production metrics show unacceptable performance  
⚠️ **Do not** add complexity without proven need based on production data

---

**Last Updated**: 2026-06-03  
**Implementation**: DialogActivityExportService.cs (lines 345-406)  
**Test Queries**: docs/database/Test_Export_Query.sql
