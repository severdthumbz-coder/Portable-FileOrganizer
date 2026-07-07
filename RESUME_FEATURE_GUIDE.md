# Resume Feature User Guide
## Portable File Organizer v5.0 Build 1.0.4

---

## What is the Resume Feature?

The **Resume Feature** protects your file operations from interruptions. If the application crashes, your computer loses power, or you accidentally close the app during a file operation, you won't lose your progress.

When you reopen the application, you'll have three choices:
1. **Resume** - Continue from where you left off
2. **Undo** - Reverse what was already done
3. **Cancel** - Start fresh and discard the incomplete operation

---

## How It Works

### Automatic Protection

The app automatically saves your progress **every 10 files** during Move or Copy operations. This happens silently in the background without slowing down your file operations.

### What Gets Saved

- Which files have been processed
- Which files are still waiting
- Source and destination folders
- Operation type (Move or Copy)
- Progress percentage

### Where It's Saved

Your progress is saved to:
```
C:\Users\[YourUsername]\AppData\Roaming\PortableFileOrganizer\resume_state.json
```

This file is automatically created and deleted as needed. You never need to interact with it directly.

---

## The Resume Dialog

When you reopen the app after an interruption, you'll see this dialog:

```
┌─────────────────────────────────────────────────┐
│    ⚠  Incomplete Operation Detected             │
│                                                  │
│    Interrupted 5 minutes ago                     │
├─────────────────────────────────────────────────┤
│                                                  │
│    Operation:      Move                          │
│    Progress:       342 of 1,000 files (34.2%)    │
│    ──────────────────────────────── 34%          │
│    Source:         C:\Downloads\Unsorted         │
│    Destination:    D:\Organized\Files            │
│    Remaining:      658 files                     │
│                                                  │
├─────────────────────────────────────────────────┤
│                                                  │
│    What would you like to do?                    │
│                                                  │
│    • Resume - Continue from where interrupted    │
│    • Undo - Reverse all moved/copied files       │
│    • Cancel - Ignore and start fresh             │
│                                                  │
├─────────────────────────────────────────────────┤
│                                                  │
│    [  🔄 Resume  ] [  ↩ Undo  ] [  ✖ Cancel  ]  │
│                                                  │
└─────────────────────────────────────────────────┘
```

---

## Your Three Options

### Option 1: Resume 🔄

**What it does**: Continues processing from where the operation stopped

**When to use**:
- You want to complete the file operation
- The interruption was unintentional (crash, power failure)
- You have time to let it finish now

**What happens**:
1. The app loads exactly where it left off
2. Already-processed files are skipped (not redone)
3. Remaining files are processed
4. Progress shows combined total (done + resuming)
5. State file is deleted when complete

**Example**:
```
Original: 1,000 files to move
Interrupted: After 342 files
Resume: Processes remaining 658 files
Result: All 1,000 files moved successfully
```

---

### Option 2: Undo ↩

**What it does**: Reverses all files that were already moved/copied

**When to use**:
- You realized you made a mistake
- You want to start over with different settings
- You want to cancel the operation and restore original state

**Important Limitations**:
- **Only works for Move operations**
- Copy operations cannot be undone (files are in both places)
- If you delete/move files manually, undo may fail for those files

**What happens**:
1. All moved files are moved back to their original locations
2. Original folder structure is recreated
3. Progress shows undo status
4. State file is deleted when complete

**Example**:
```
Original: 500 files moved from C:\Downloads to D:\Archive
Interrupted: After 234 files moved
Undo: 234 files moved back to C:\Downloads
Result: Back to original state before operation started
```

---

### Option 3: Cancel ✖

**What it does**: Discards the incomplete operation state and starts fresh

**When to use**:
- You've manually fixed the files yourself
- You want to organize the files differently
- The operation is no longer relevant

**What happens**:
1. Resume state file is deleted
2. No files are moved or changed
3. App starts clean as if nothing happened

**Note**: This doesn't undo anything - files that were already moved/copied stay where they are.

---

## Common Scenarios

### Scenario 1: Power Outage During Large Move

**Situation**:
- Moving 10,000 photos from external drive to organized folders
- Power goes out after 4,523 photos moved
- Computer restarts

**What to do**:
1. Reconnect external drive
2. Open Portable File Organizer
3. Resume dialog appears
4. Click **Resume**
5. Remaining 5,477 photos are processed
6. Operation completes successfully

**Result**: All 10,000 photos organized, no duplicates, no re-processing

---

### Scenario 2: Accidental Close During Copy

**Situation**:
- Copying 2,000 documents to backup drive
- Accidentally close application at 1,234 files
- Immediately realize mistake

**What to do**:
1. Reopen application immediately
2. Dialog shows "Interrupted less than a minute ago"
3. Click **Resume**
4. Remaining 766 files are copied
5. Backup completes

**Result**: Full backup with minimal delay

---

### Scenario 3: Wrong Destination Selected

**Situation**:
- Start moving files to wrong folder
- Realize after 156 files moved
- Close app to stop operation

**What to do**:
1. Reopen application
2. Dialog appears showing 156 files completed
3. Click **Undo**
4. All 156 files moved back to source
5. Start over with correct destination

**Result**: Original state restored, ready to try again correctly

---

### Scenario 4: Application Crash Mid-Operation

**Situation**:
- Moving 5,000 files
- Application crashes at file 2,891 (unknown reason)
- State saved at file 2,880 (last save point)

**What to do**:
1. Reopen application
2. Dialog shows 2,880 files completed
3. Click **Resume**
4. Processing restarts from file 2,881
5. Files 2,881-2,890 are re-processed (already moved, will be skipped based on conflict resolution)
6. Files 2,891-5,000 are processed
7. Operation completes

**Result**: All files processed with minimal duplication based on conflict settings

---

## Technical Details

### Save Frequency
- State saved **every 10 files**
- Atomic writes prevent corruption
- Non-blocking (doesn't slow operations)

### What If Multiple Interruptions?
The resume feature works multiple times:
1. Start operation (1,000 files)
2. Crash at 250 files → Save state
3. Resume, crash at 500 files → Update state
4. Resume, crash at 750 files → Update state
5. Resume, complete successfully

Each resume picks up where the last one left off.

### State Validation
On startup, the app checks if:
- Source folder still exists
- Destination folder still exists
- Files haven't been moved manually
- JSON file isn't corrupted

If validation fails, state is automatically cleaned up and you start fresh.

---

## Frequently Asked Questions

### Q: What if I moved some files manually during the interruption?

**A**: Resume will skip files that already exist in the destination (based on your conflict resolution setting). Files moved manually won't cause errors.

### Q: Can I resume after restarting my computer?

**A**: Yes! The state file persists across reboots as long as it's on the same computer.

### Q: How long does the state file stay?

**A**: Forever, until you Resume/Undo/Cancel, or until you run a new operation (which overwrites it).

### Q: What happens if I start a new operation without handling the old one?

**A**: The dialog appears **immediately on startup**, before you can do anything else. You must choose Resume/Undo/Cancel before proceeding.

### Q: Can I undo a Copy operation?

**A**: No. Copy operations leave files in both places, so there's no clear "undo" action. You can manually delete copied files if needed.

### Q: Does resume work with all operations?

**A**: Only Move and Copy operations support resume. Scan, Dry Run, and Detect Duplicates complete quickly so they don't need resume.

### Q: What if the external drive is disconnected?

**A**: State validation will fail and you'll be notified. Reconnect the drive and try again.

### Q: Is there a timeout for old resume states?

**A**: Not currently. Even if interrupted months ago, the state remains. (May be added in future versions)

---

## Best Practices

### ✅ Do This
- Let large operations complete if possible
- Keep external drives connected during operations
- Use Resume if accidentally interrupted
- Use Undo if you made a mistake

### ❌ Avoid This
- Don't manually move files during an interrupted operation
- Don't delete source folders before undoing
- Don't disconnect drives before operation completes
- Don't ignore resume dialog without choosing an option

---

## Troubleshooting

### Resume Dialog Doesn't Appear

**Possible Causes**:
- No incomplete operation
- State file was manually deleted
- State file is corrupted (automatically cleaned up)

**Solution**: Nothing to worry about - start a new operation normally

---

### Resume Fails with Error

**Possible Causes**:
- Source folder was deleted
- Destination folder was deleted
- Files were manually moved
- Disk is full

**Solution**:
1. Check that source and destination folders exist
2. Ensure disk has free space
3. If problem persists, click Cancel and start fresh

---

### Undo Doesn't Restore All Files

**Possible Causes**:
- Some files were manually deleted
- Destination folder was modified
- File permissions changed

**Solution**:
- Check what files remain in destination
- Manually move any stragglers
- Files that couldn't be restored are reported in the result dialog

---

## Safety & Data Integrity

### Protection Against Corruption
- **Atomic Writes**: State file uses two-step write (temp file → rename)
- **Validation**: State checked before use
- **Auto-Cleanup**: Corrupted states automatically deleted

### What Can't Be Lost
- File data (files themselves are never modified incorrectly)
- Destination files (conflict resolution prevents overwrites)

### What Could Be Re-Done
- If state saves at file 100 but crash at file 110
- Files 101-110 may be re-processed on resume
- Conflict resolution handles this gracefully

---

## Version Information

**Feature Introduced**: Build 1.0.4  
**Last Updated**: March 10, 2026  
**Supported Operations**: Move, Copy  
**Platform**: Windows 10/11  

---

## Summary

The Resume Feature makes Portable File Organizer **safe for enterprise use**. You can now:

✅ Process tens of thousands of files safely  
✅ Recover from power failures and crashes  
✅ Undo mistakes  
✅ Run overnight operations confidently  
✅ Never lose progress  

**Your file operations are now bulletproof!** 🛡️

---

*For technical implementation details, see BUILD_1.0.4_CHANGELOG.md*
