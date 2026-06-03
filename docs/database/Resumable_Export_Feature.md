# Resumable Export Feature

## Overview

The Dialog Activity Export tool now supports **resumable exports** with automatic checkpoint persistence. If an export is interrupted (network failure, system restart, etc.), it can resume from where it left off instead of starting over.

## Features

### 1. Automatic Checkpointing

- Checkpoint saved every 10 batches automatically
- Checkpoint includes:
  - Issue number
  - Cutoff timestamp
  - Total rows processed
  - Current batch number
  - Last cursor position (CorrespondenceId, Status)
  - Checkpoint timestamp

### 2. Automatic Resume

- On restart, tool automatically detects existing checkpoint
- Validates checkpoint matches current parameters (issue, cutoff)
- Resumes from last saved position
- Appends to existing output file

### 3. Fresh Start Option

- Use `--fresh` or `-f` flag to ignore checkpoints
- Deletes existing checkpoint file
- Starts export from beginning
- Overwrites existing output file

## Usage

### Normal Export (with auto-resume)

```bash
# First run (or continue from checkpoint if exists)
dotnet run -- --issue 1716 \
  --output C:\temp\export.csv \
  --cutoff "2026-02-15 00:00:00" \
  --azure-ad \
  --yes

# If interrupted, run same command to resume
# Tool automatically detects checkpoint and continues
```

### Force Fresh Start

```bash
# Ignore any existing checkpoint and start fresh
dotnet run -- --issue 1716 \
  --output C:\temp\export.csv \
  --cutoff "2026-02-15 00:00:00" \
  --fresh \
  --azure-ad \
  --yes
```

## Checkpoint File

### Location
Checkpoint file is stored alongside the output CSV:
- Output: `C:\temp\export.csv`
- Checkpoint: `C:\temp\export.csv.checkpoint`

### Format
JSON format with export state:
```json
{
  "IssueNumber": 1716,
  "CutoffTimestamp": "2026-02-15T00:00:00",
  "TotalProcessed": 50000,
  "BatchNumber": 5,
  "LastCorrespondenceId": "a1b2c3d4-...",
  "LastStatus": 4,
  "CheckpointTime": "2026-06-03T15:30:00Z"
}
```

### Lifecycle
1. **Created**: After processing first batch set (every 10 batches)
2. **Updated**: Every 10 batches during export
3. **Deleted**: Automatically when export completes successfully
4. **Deleted**: Manually with `--fresh` flag

## Resume Behavior

### Conditions for Resume

Resume occurs when ALL conditions are met:
1. ✅ Checkpoint file exists
2. ✅ Output CSV file exists
3. ✅ Issue number matches
4. ✅ Cutoff timestamp matches
5. ✅ `--fresh` flag NOT used

### What Happens on Resume

1. **Loads checkpoint**: Reads cursor position and progress
2. **Opens output file**: In append mode (preserves existing data)
3. **Skips CSV header**: Already written in first run
4. **Continues from cursor**: Picks up where it left off
5. **Updates progress**: Shows cumulative progress including resumed work

### Resume Logging

```
info: Resuming export from checkpoint: 50,000 rows, batch 5
info: Resuming export for Issue #1716 to C:\temp\export.csv
```

## Unlimited Batching

### Test Mode vs Production

**Test Mode** (`--max-batches N`):
- Limits export to N batches
- Saves checkpoint at end
- Can resume to process more batches
- Useful for testing and validation

**Production Mode** (no `--max-batches`):
- Processes ALL records until complete
- No batch limit
- Saves checkpoints every 10 batches
- Deletes checkpoint when done

### Example: Incremental Testing

```bash
# Test with 2 batches
dotnet run -- --issue 1716 --output test.csv --cutoff "2026-02-15" --max-batches 2 --azure-ad --yes
# Output: test.csv (1000 rows), test.csv.checkpoint (saved)

# Resume and process 2 more batches
dotnet run -- --issue 1716 --output test.csv --cutoff "2026-02-15" --max-batches 4 --azure-ad --yes
# Output: test.csv (2000 rows total), test.csv.checkpoint (updated to batch 4)

# Resume and complete full export (no limit)
dotnet run -- --issue 1716 --output test.csv --cutoff "2026-02-15" --azure-ad --yes
# Output: test.csv (all rows), test.csv.checkpoint (deleted when complete)
```

## Error Handling

### Corrupt Checkpoint

If checkpoint file is corrupted:
- Tool logs warning
- Ignores checkpoint
- Starts fresh export
- User data is NOT corrupted (checkpoint is separate)

### Parameter Mismatch

If checkpoint doesn't match current parameters:
- Tool ignores checkpoint
- Starts fresh export
- Example: Issue 1716 checkpoint, but running Issue 1951

### Interrupted Resume

If resume is interrupted again:
- Checkpoint is updated during resume
- Can resume multiple times
- Each resume appends to existing file

## Production Recommendations

### Large Exports (Full Dataset)

```bash
# Run without --max-batches to process everything
dotnet run -- --issue 1951 \
  --output production_1951.csv \
  --cutoff "2026-05-19 11:35:59" \
  --batch-size 50000 \
  --azure-ad \
  --yes

# If process is killed/interrupted, run same command to resume
```

### Monitoring Progress

- Checkpoint files can be inspected to track progress
- `TotalProcessed` shows rows completed
- `BatchNumber` shows batches completed
- `CheckpointTime` shows last save time

### Cleanup

```bash
# If you need to restart from scratch
dotnet run -- --issue 1951 \
  --output production_1951.csv \
  --cutoff "2026-05-19 11:35:59" \
  --fresh \
  --azure-ad \
  --yes
```

## Implementation Details

### Checkpoint Interval

- Default: Every 10 batches
- At 50,000 rows/batch = checkpoint every 500,000 rows
- Checkpoint saved before exiting test mode

### File Operations

- **Append Mode**: Output file opened in append mode on resume
- **Write Buffer**: 64KB buffer for efficient writes
- **Flush After Batch**: Data flushed to disk after each batch

### Cursor Pagination

- Cursor stored as `(CorrespondenceId, Status)` tuple
- Deterministic ordering ensures no gaps or duplicates
- Next batch starts: `WHERE (CorrespondenceId, Status) > (lastId, lastStatus)`

## Benefits

1. **Fault Tolerance**: Recover from interruptions without data loss
2. **Long-Running Jobs**: Export can span multiple sessions/days
3. **Incremental Testing**: Test in small batches, resume to complete
4. **Resource Management**: Pause and resume based on system load
5. **No Duplicate Data**: Cursor ensures exact continuation

## Limitations

- Resume only works for single-issue exports (`--issue 1716` or `--issue 1951`)
- Combined exports (`--issue all`) do not support resume (yet)
- Checkpoint file must not be manually edited
- Changing parameters (issue, cutoff) requires `--fresh` restart

## Troubleshooting

### "Resuming export from checkpoint" but I want fresh start
**Solution**: Use `--fresh` flag

### Checkpoint exists but resume not happening
**Check**: Issue number and cutoff timestamp must match exactly

### Want to see checkpoint contents
```bash
Get-Content C:\temp\export.csv.checkpoint | ConvertFrom-Json
```

### Delete checkpoint manually
```bash
Remove-Item C:\temp\export.csv.checkpoint
```
