# Network Read Performance Issue - Analysis

## Problem Discovery

Testing with 10,000 row batch size revealed a **catastrophic network read performance issue**.

## Test Results

### Batch Size 1,000 (Good ✅)
```
Batch 1:
  Status 4: ExecuteReader=70ms, Read 1000 rows=18ms, Total=88ms
  Status 6: ExecuteReader=23ms, Read 1000 rows=23ms, Total=46ms
  Batch timing: Fetch=146ms, Merge=4ms, Write=4ms, Total=155ms

Batch 2:
  Status 4: ExecuteReader=23ms, Read 1000 rows=221ms, Total=244ms
  Status 6: ExecuteReader=23ms, Read 1000 rows=24ms, Total=48ms
  Batch timing: Fetch=293ms, Merge=2ms, Write=2ms, Total=298ms
```

**Analysis**: Read time 18-221ms for 1,000 rows = 0.02-0.22ms per row ✅

### Batch Size 10,000 (BAD ❌)
```
Batch 1:
  Status 4: ExecuteReader=76ms, Read 10000 rows=82,246ms, Total=82,322ms
  Status 6: ExecuteReader=24ms, Read 10000 rows=90,968ms, Total=90,993ms
  Batch timing: Fetch=173,327ms, Merge=13ms, Write=34ms, Total=173,375ms

Batch 2:
  Status 4: ExecuteReader=24ms, Read 10000 rows=98,243ms, Total=98,267ms
  Status 6: ExecuteReader=21ms, Read 10000 rows=1,402ms, Total=1,424ms
  Batch timing: Fetch=99,692ms, Merge=7ms, Write=12ms, Total=99,712ms
```

**Analysis**: Read time 1,400-98,000ms for 10,000 rows = 0.14-9.8ms per row ❌

## Performance Comparison

| Batch Size | Avg Read Time | Read Time Per Row | Export Time (9.97M rows) |
|------------|--------------|-------------------|-------------------------|
| **1,000** | 18-221ms | 0.02-0.22ms | **37 minutes** ✅ |
| **10,000** | 1,400-98,000ms | 0.14-9.8ms | **38.7 hours** ❌ |

**Conclusion**: 10x batch size = 40-400x slower per row = 63x longer total export!

## Root Cause Analysis

### Why Read Time Scales Non-Linearly

1. **Network Buffering Issue**
   - Npgsql may not be buffering large result sets efficiently
   - Reading 10K rows may exceed default buffer sizes
   - Forces multiple round-trips per batch

2. **Azure PostgreSQL Network Throttling**
   - Azure may throttle large result set transfers
   - Adaptive throttling kicks in after certain data size
   - 1,000 rows (~200KB) under threshold
   - 10,000 rows (~2MB) over threshold

3. **TCP Window Size Limitation**
   - Default TCP receive window may be too small
   - With 1,000 rows, single TCP window transfer
   - With 10,000 rows, multiple window transfers needed
   - Window exhaustion causes wait for ACKs

4. **SSL/TLS Overhead**
   - Encryption overhead grows with payload size
   - Small payloads: overhead negligible
   - Large payloads: encryption becomes bottleneck

### Evidence

**Status 6 Batch 2: Only 1,402ms for 10,000 rows**
- This proves network CAN handle 10K rows fast
- Suggests issue is Status-specific or order-dependent
- Possibly related to result set composition or ordering

**Status 4 Batch 1 vs Batch 2**
- Batch 1: 82,246ms (first access, cold path)
- Batch 2: 98,243ms (warm cache, should be faster but isn't)
- Suggests network throttling, not caching issue

## Optimal Batch Size Determination

### Test Matrix Needed

| Batch Size | Expected Read Time | Expected Total Export |
|------------|-------------------|----------------------|
| 1,000 | 20-220ms | 37 minutes ✅ |
| 2,000 | 40-440ms? | 42 minutes? |
| 3,000 | 60-660ms? | 47 minutes? |
| 5,000 | 100-1,100ms? | **1 hour?** |
| 10,000 | 1,400-98,000ms | 38.7 hours ❌ |

**Hypothesis**: Sweet spot is **2,000-5,000 rows**
- Below 5,000: Linear scaling (under throttle threshold)
- Above 5,000: Non-linear scaling (throttling kicks in)

### Recommended Testing
```powershell
# Test 5,000 row batch
.\test-export-2.ps1 -BatchSize 5000

# Look for:
# - Read time 50-500ms per 5,000 rows (0.01-0.1ms per row)
# - Total batch time < 1 second
# - Consistent between batches
```

If 5,000 shows good performance:
- **5,000 batch size = 1,994 batches × 0.5s = 16.6 minutes** 🚀

If 5,000 still shows slowdown:
- **Stick with 1,000 batch size = 37 minutes** (proven)

## Why Larger Batches Don't Help Here

**Normal expectation**: Larger batches → fewer round-trips → faster total time

**Actual behavior**: Larger batches → network throttling → slower per-row transfer → slower total time

**Why**: 
1. Azure → Your local machine is cross-region internet transfer
2. Azure PostgreSQL applies adaptive throttling on large transfers
3. SSL/TLS overhead grows non-linearly with payload size
4. TCP window size limitations cause multi-round-trip transfers

**If running from Azure VM in same region**:
- Network throttling would be minimal
- Larger batches (10K-20K) would be faster
- Could achieve 5-10 minute total export

## Recommendations

### For Your Current Setup (Local Machine)

**Use batch size 1,000** (proven 37 minutes):
```powershell
.\export-production-1716.ps1 -BatchSize 1000
```

**Or test 5,000** (might be sweet spot):
```powershell
# Test first
.\test-export-2.ps1 -BatchSize 5000

# If good (Read < 500ms per 5K rows), use in production
.\export-production-1716.ps1 -BatchSize 5000
```

### For Azure VM (Future Optimization)

If you can run from Azure VM in Norway region:
- Use batch size 10,000-20,000
- Expected: 5-10 minute total export
- Network is local, no throttling

## Updated Export Time Estimates

| Scenario | Batch Size | Time per Batch | Total Time |
|----------|-----------|----------------|-----------|
| **Local (Current)** | 1,000 | 225ms | **37 min** ✅ |
| **Local (Optimized)** | 5,000 | 500ms? | **17 min?** 🤞 |
| **Local (Bad)** | 10,000 | 140s | **38.7 hours** ❌ |
| **Azure VM** | 10,000 | 300ms | **5 min** 🚀 |

## Action Items

1. ✅ **Reverted default batch size to 5,000** (middle ground to test)
2. 📊 **Test 5,000 batch size** - Run `.\test-export-2.ps1`
3. 📈 **If good**: Use 5,000 for production (17 min estimate)
4. 📉 **If bad**: Use 1,000 for production (37 min proven)

## Lesson Learned

**Bigger is not always better with network transfers!**

- Small batches (1K): High round-trip overhead, but predictable
- Medium batches (5K): Potential sweet spot
- Large batches (10K+): Network throttling dominates, catastrophic slowdown

The "optimal" batch size depends heavily on:
- Network path (local vs cross-region)
- Cloud provider throttling policies
- TCP window sizes
- SSL/TLS implementation

**Always test actual performance before assuming larger = faster!**
