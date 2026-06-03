# DialogActivityExporter - Quick Reference Card

## 🚀 Fastest Way to Test (~2 seconds)

```powershell
cd tools\Altinn.Correspondence.DialogActivityExporter
.\test-export.ps1
```
✅ Auto-opens CSV file for verification (1000 rows)

---

## 📋 Common Test Commands

### Test Issue #1951 (1000 rows)
```powershell
.\test-export.ps1 -Issue 1951 -BatchSize 1000
```

### Test Issue #1716 (5000 rows)
```powershell
.\test-export.ps1 -Issue 1716 -BatchSize 5000
```

### Test Both Issues
```powershell
.\test-export.ps1 -Issue all -BatchSize 2000
```

### Custom Output Path
```powershell
.\test-export.ps1 -OutputPath C:\temp\my_test.csv
```

**Note:** Minimum batch size is 1000 rows (enforced by exporter)

---

## 🔍 Expected Output Format

```csv
DialogId,DialogActivityId,Timestamp,ActorId,ActorName,ActivityType
123,abc-def-456,2024-01-15 10:30:00,12345678,Test Org,Read
123,ghi-jkl-789,2024-01-16 14:22:13,12345678,Test Org,Confirmed
```

**Columns:**
- `DialogId` - Altinn2CorrespondenceId (integer)
- `DialogActivityId` - UUID of CorrespondenceStatus
- `Timestamp` - StatusChanged or SyncedFromAltinn2
- `ActorId` - From A2Parties (0 if null)
- `ActorName` - From A2Parties (empty if null)
- `ActivityType` - "Read" (Status 4) or "Confirmed" (Status 6)

---

## ⚡ Performance (with indexes)

| Rows | Batch Size | Time |
|------|-----------|------|
| 1,000 | 1000 | ~2s |
| 5,000 | 5000 | 3-5s |
| 10,000 | 10000 | 5-8s |
| 100,000 | 50000 | 15-30s |

---

## 🛠️ Troubleshooting

### No connection string / Azure credentials not found
**Fix:** 
```powershell
# Option A: Use connection string
.\test-export.ps1 -ConnectionString "Host=server;Database=correspondence;Username=user;Password=pass"

# Option B: Edit appsettings.json
notepad appsettings.json
# Add: "ConnectionString": "Host=..."

# Option C: Configure Azure authentication
# Azure CLI:
az login

# Or use Visual Studio / VS Code authentication
# Tools → Options → Azure Service Authentication (VS)
# Or Azure Account extension (VS Code)
```

### Export too long
**Fix:** Press **Ctrl+C** (partial results already saved)

### Empty file
**Fix:** Check cutoff date - try `--cutoff "2026-12-31 23:59:59"`

### Wrong issue number
**Fix:** Use `--issue 1951`, `--issue 1716`, or `--issue all`

### Batch size error
**Fix:** Minimum is 1000 rows - use `.\test-export.ps1 -BatchSize 1000`

---

## 📖 Full Documentation

- **Testing_Guide.md** - Complete testing guide
- **test-export.ps1** - PowerShell script source
- **README.md** - Main documentation hub

---

## 🎯 Production Export (after testing)

```powershell
dotnet run -- `
  --issue all `
  --output C:\exports\production_export.csv `
  --cutoff "2026-05-19 11:35:59" `
  --oldest "2019-03-23" `
  --batch-size 50000 `
  --azure-ad `
  --yes
```

**Estimated Time:** 30-60 minutes for full export  
**Estimated Size:** 5-10 GB CSV file

---

## 💡 Tips

✅ Start with 1000 rows to verify format  
✅ Check CSV after first batch (~2 seconds)  
✅ Press Ctrl+C to stop early if needed  
✅ Use `--azure-ad` for automatic authentication  
✅ Partial results are always saved to file  
✅ Minimum batch size: 1000 rows

---

**Need Help?** See `docs\database\Testing_Guide.md` for detailed documentation
