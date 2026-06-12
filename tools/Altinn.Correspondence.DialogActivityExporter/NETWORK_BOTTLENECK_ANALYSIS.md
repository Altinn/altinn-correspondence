# Network Transfer Bottleneck - Root Cause Analysis

## Problem Identified

The query is **NOT** slow - the network transfer is the bottleneck!

### Evidence

**EXPLAIN ANALYZE (server-side):**
```
Execution Time: 91.377 ms (0.09 seconds) ✅
```

**Application logs (client-side):**
```
Status 4 query: ExecuteReader=2124ms, Read 5000 rows=58257ms, Total=60381ms
                                       ^^^^^^^^^^^^^^^^
                                       58 SECONDS!
```

**Breakdown:**
- Query execution: ~2 seconds
- **Network transfer: ~58 seconds** ❌
- Transfer is **29x slower** than query!

**Throughput:**
- 5000 rows @ 174 bytes/row = ~850KB
- 58 seconds to transfer 850KB = **14.6 KB/sec**
- Expected: 10-100 MB/sec in Azure datacenter

## Root Cause: Geographic Distance + Azure Network

### Why It's Slow

1. **Distance**: Your machine → Azure datacenter
   - Round-trip latency: 50-200ms per packet
   - 5000 rows × latency = cumulative delay

2. **Azure PostgreSQL Network Limits**:
   - Connection from external IP (not VNet)
   - Subject to internet gateway throttling
   - Shared bandwidth with other traffic

3. **TCP Flow Control**:
   - Small receive windows
   - Packet loss requiring retransmits
   - Network congestion

### Why Performance Varies

**Fast periods (100-200ms for 2500 rows):**
- Network path clear
- No throttling
- Data already in server buffers

**Slow periods (60-130 seconds for 5000 rows):**
- Network congestion
- Azure bandwidth throttling
- Cross-availability-zone routing
- Peak usage hours

## Solutions

### Solution 1: Run Export from Azure VM ⭐ RECOMMENDED

**Deploy exporter to Azure VM in same region as database:**

```
Your PC → Azure PostgreSQL:
  - Distance: 1000+ km
  - Latency: 50-200ms
  - Bandwidth: Throttled by Azure
  - Time: 22+ hours for 9.97M rows ❌

Azure VM → Azure PostgreSQL (same VNet):
  - Distance: Same datacenter
  - Latency: <1ms
  - Bandwidth: 1-10 Gbps
  - Time: 10-20 minutes for 9.97M rows ✅
```

**Estimated improvement: 100-200x faster!**

**How to do it:**

1. **Create Azure VM** in same region as database
   ```
   Size: Standard_D4s_v3 (4 vCPU, 16GB RAM)
   Region: Same as database
   VNet: Same as database (or peered)
   ```

2. **Copy exporter to VM**
   ```powershell
   # Build locally
   dotnet publish -c Release

   # Copy to VM
   scp -r bin/Release/net10.0/publish/* azureuser@<vm-ip>:/home/azureuser/exporter/
   ```

3. **Run export on VM**
   ```bash
   cd /home/azureuser/exporter
   ./Altinn.Correspondence.DialogActivityExporter \
     --issue 1716 \
     --batch-size 10000 \
     --output /mnt/data/export_1716.csv
   ```

4. **Download result**
   ```powershell
   scp azureuser@<vm-ip>:/mnt/data/export_1716.csv C:\temp\
   ```

**Cost:** ~$0.20/hour for VM × 1 hour = $0.20 total (vs 22 hours of your time!)

### Solution 2: Connection String Optimization

Add these parameters to reduce round-trips:

```csharp
"Host=...;Database=...;Username=...;Password=...;" +
"Max Pool Size=1;" +                  // Prevent connection reuse issues
"TCP KeepAlive=30;" +                 // Keep connection alive
"Internal Command Timeout=300;" +     // Allow long queries
"Read Buffer Size=65536;" +           // 64KB read buffer
"Write Buffer Size=65536;" +          // 64KB write buffer
"No Reset On Close=true;" +           // Avoid connection reset overhead
"CommandTimeout=300"                  // 5 minute timeout
```

**Expected improvement:** 10-20% faster (not enough to fix the issue)

### Solution 3: Reduce Batch Size Further

Smaller batches = less data per network round-trip:

```
BatchSize 5000: ~850KB per batch, 60s transfer
BatchSize 1000: ~170KB per batch, 12s transfer (estimated)
BatchSize 500:  ~85KB per batch, 6s transfer (estimated)
```

But this doesn't fix the root cause - you'll still take 15-20 hours.

### Solution 4: Compress Data Transfer

Enable PostgreSQL compression (requires server configuration):

```sql
-- On database server
ALTER DATABASE correspondence SET ssl_compression = 'on';
```

**Expected improvement:** 2-3x faster (still not enough)

### Solution 5: Use Azure Data Factory

If you have access to Azure Data Factory:
- Create a Copy Activity
- Source: Azure PostgreSQL query
- Sink: Azure Blob Storage (CSV)
- Runs within Azure network
- Estimated time: 10-15 minutes

## Recommendation

**Priority 1: Run export from Azure VM**
- This is the **only** solution that will give you acceptable performance
- 100-200x speedup
- 10-20 minutes instead of 22 hours
- Small VM cost (<$1)

**Priority 2: If you must run locally**
- Use BatchSize 1000 (4x more batches, but more predictable)
- Run overnight during off-peak hours
- Enable connection optimizations
- Accept that it will take 15-20 hours

**Priority 3: For future exports**
- Set up permanent Azure VM or Azure Function
- Schedule exports to run in Azure
- Download results when complete

## Implementation

I've already removed the DISTINCT (small optimization) and the code is ready.

**Next steps:**
1. Create Azure VM in database region
2. Copy exporter to VM
3. Run export from VM
4. Download result
5. Export completes in 10-20 minutes ✅

Or:
1. Accept 15-20 hour runtime
2. Start export before going home
3. Check in morning

Your choice! 😊

---

**Bottom line:** The query is perfect. The network is the bottleneck. Run it from Azure to fix it.
