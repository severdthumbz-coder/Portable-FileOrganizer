# Build 1.2.0 - Comprehensive Test Plan

**Test Date:** March 16, 2026  
**Build:** v5.0 Build 1.2.0  
**Focus:** Duplicate Management System  
**Estimated Test Time:** 45-60 minutes

---

## 🎯 TESTING OBJECTIVES

1. ✅ Verify all 8 new features work correctly
2. ✅ Ensure no regressions in existing features
3. ✅ Validate safety mechanisms
4. ✅ Confirm UI quality and responsiveness
5. ✅ Test edge cases and error handling

---

## 📋 PRE-TEST SETUP

### Requirements:
- ✅ Windows 10/11
- ✅ .NET 9.0 Runtime
- ✅ Test data prepared

### Test Data Setup:

**Option A: Quick Test (5 minutes)**
```
1. Create folder: C:\DuplicateTest
2. Create file: test.txt (any content)
3. Copy test.txt 3 times:
   - C:\DuplicateTest\test.txt
   - C:\DuplicateTest\Copy1\test.txt
   - C:\DuplicateTest\Copy2\test.txt
4. Result: 1 group, 3 files, 2 duplicates
```

**Option B: Realistic Test (15 minutes)**
```
1. Create folder: C:\PhotoTest
2. Download 10-20 sample images
3. Copy each image 2-3 times to different subfolders
4. Vary modification dates (edit properties)
5. Result: Multiple groups, realistic scenario
```

**Option C: Large Scale Test (30 minutes)**
```
1. Use existing photo library or document folder
2. 1,000+ files recommended
3. Natural duplicates from backups/downloads
4. Result: Real-world testing
```

---

## 🧪 TEST SCENARIOS

---

### TEST 1: Duplicates Tab Visibility ⭐ CRITICAL

**Purpose:** Verify new tab exists and is properly positioned

**Steps:**
1. Launch FileOrganizer.exe
2. Look at tab bar

**Expected Results:**
- ✅ "🔁 Duplicates" tab appears
- ✅ Tab positioned between "History" and "Help"
- ✅ Clicking tab shows Duplicates interface
- ✅ All sections visible (Detection, Summary, Selection, Groups, Actions)

**Pass/Fail:** ___________

**Notes:** ___________________________________________

---

### TEST 2: Detect Duplicates (Full Scan) ⭐ CRITICAL

**Purpose:** Verify SHA256 duplicate detection works

**Steps:**
1. Go to Duplicates tab
2. Ensure "Full Scan (SHA256)" is selected
3. Browse to test folder (e.g., C:\DuplicateTest)
4. Click "Detect Duplicates"

**Expected Results:**
- ✅ Progress bar appears and updates
- ✅ Status shows "Scanning for duplicates... X% complete"
- ✅ Scan completes successfully
- ✅ Toast notification appears
- ✅ Summary statistics populate:
   - Duplicate Groups: [Expected number]
   - Duplicate Files: [Expected number]
   - Wasted Space: [Expected size]
- ✅ Groups appear in scrollable list
- ✅ MessageBox shows results summary

**Pass/Fail:** ___________

**Performance:** Scan Duration: _________ seconds

**Notes:** ___________________________________________

---

### TEST 3: Detect Duplicates (Quick Scan) ⭐ CRITICAL

**Purpose:** Verify size-based detection works and is faster

**Steps:**
1. Clear previous results (re-launch app if needed)
2. Go to Duplicates tab
3. Select "Quick Scan (Size Only)"
4. Browse to same test folder
5. Click "Detect Duplicates"

**Expected Results:**
- ✅ Scan completes MUCH faster than full scan
- ✅ Results appear (may have more false positives)
- ✅ Can still manage results normally

**Pass/Fail:** ___________

**Performance:** 
- Full Scan Time: _________ seconds
- Quick Scan Time: _________ seconds
- Speedup: _________ x faster

**Notes:** ___________________________________________

---

### TEST 4: Expand/Collapse Groups ⭐ CRITICAL

**Purpose:** Verify group visibility controls work

**Steps:**
1. After detection, locate a duplicate group
2. Click the expand/collapse button (▶/▼)
3. Repeat for multiple groups

**Expected Results:**
- ✅ Groups start expanded (▼ symbol)
- ✅ Clicking ▼ collapses group (hides files)
- ✅ Symbol changes to ▶
- ✅ Clicking ▶ expands group (shows files)
- ✅ Symbol changes to ▼
- ✅ Each group operates independently
- ✅ Smooth animation/transition

**Pass/Fail:** ___________

**Notes:** ___________________________________________

---

### TEST 5: Group Display Information ⭐ CRITICAL

**Purpose:** Verify group header shows correct information

**Steps:**
1. Expand a duplicate group
2. Examine group header and file details

**Expected Results:**
- ✅ Group header shows: "filename (X copies, Y wasted)"
- ✅ Each file shows:
   - Checkbox (unchecked by default)
   - Star icon (⭐) if recommended to keep
   - Full file path
   - Created and Modified dates
- ✅ File information is accurate

**Pass/Fail:** ___________

**Notes:** ___________________________________________

---

### TEST 6: Smart Selection - Keep Newest ⭐ CRITICAL

**Purpose:** Verify "Keep Newest" auto-selection works

**Steps:**
1. After detection, locate "Auto-select strategy" dropdown
2. Select "Keep Newest"
3. Expand groups to verify

**Expected Results:**
- ✅ Dropdown changes to "Keep Newest"
- ✅ For each group:
   - File with NEWEST modification date gets ⭐
   - All OTHER files get checked ☑
- ✅ Selection statistics update:
   - "Selected to Delete" increases
   - Space calculation updates
- ✅ Visual feedback immediate

**Pass/Fail:** ___________

**Example Verification:**
```
Group: test.txt
- test.txt (Modified: Jan 1) → ☑ Checked
- Copy1\test.txt (Modified: Feb 1) → ☑ Checked  
- Copy2\test.txt (Modified: Mar 1) → ☐ ⭐ Keep (newest)

Correct? Yes / No
```

**Notes:** ___________________________________________

---

### TEST 7: Smart Selection - Keep Oldest ⭐ CRITICAL

**Purpose:** Verify "Keep Oldest" auto-selection works

**Steps:**
1. Change dropdown to "Keep Oldest"
2. Expand groups to verify

**Expected Results:**
- ✅ For each group:
   - File with OLDEST modification date gets ⭐
   - All OTHER files get checked ☑
- ✅ Selection reverses from "Keep Newest"
- ✅ Statistics update correctly

**Pass/Fail:** ___________

**Notes:** ___________________________________________

---

### TEST 8: Smart Selection - Keep Shortest Path

**Purpose:** Verify path length selection works

**Steps:**
1. Change dropdown to "Keep Shortest Path"
2. Expand groups to verify

**Expected Results:**
- ✅ For each group:
   - File with SHORTEST path gets ⭐
   - Example: "C:\test.txt" kept over "C:\Folder\Subfolder\test.txt"
- ✅ Statistics update correctly

**Pass/Fail:** ___________

**Notes:** ___________________________________________

---

### TEST 9: Smart Selection - Keep Longest Path

**Purpose:** Verify longest path selection works

**Steps:**
1. Change dropdown to "Keep Longest Path"
2. Expand groups to verify

**Expected Results:**
- ✅ For each group:
   - File with LONGEST path gets ⭐
   - Example: "C:\Folder\Subfolder\test.txt" kept over "C:\test.txt"
- ✅ Statistics update correctly

**Pass/Fail:** ___________

**Notes:** ___________________________________________

---

### TEST 10: Manual Selection ⭐ CRITICAL

**Purpose:** Verify manual checkbox selection works

**Steps:**
1. Set dropdown to "None"
2. Manually check/uncheck various files
3. Watch statistics update

**Expected Results:**
- ✅ Can click any checkbox
- ✅ Checkboxes toggle correctly
- ✅ Stars remain independent of selection
- ✅ Statistics update in real-time:
   - Each check increases count
   - Each uncheck decreases count
   - Space calculation accurate
- ✅ Can select any combination

**Pass/Fail:** ___________

**Notes:** ___________________________________________

---

### TEST 11: Clear Selection

**Purpose:** Verify selection clearing works

**Steps:**
1. Apply smart selection (any strategy)
2. Click "Clear Selection" button

**Expected Results:**
- ✅ All checkboxes unchecked
- ✅ All stars removed
- ✅ Dropdown resets to "None"
- ✅ Statistics reset:
   - Selected to Delete: 0 files (0.00 GB)

**Pass/Fail:** ___________

**Notes:** ___________________________________________

---

### TEST 12: Delete Selected - Validation ⭐ CRITICAL

**Purpose:** Verify safety validation prevents deleting all copies

**Steps:**
1. Expand a group
2. Check ALL files in the group
3. Click "Delete Selected"

**Expected Results:**
- ✅ Error dialog appears
- ✅ Message: "Error: All copies of a file cannot be deleted"
- ✅ Message: "At least one copy must be kept in each group"
- ✅ Operation blocked
- ✅ No files deleted

**Pass/Fail:** ___________

**Notes:** ___________________________________________

---

### TEST 13: Delete Selected - Confirmation ⭐ CRITICAL

**Purpose:** Verify confirmation dialog before deletion

**Steps:**
1. Apply "Keep Newest" strategy
2. Click "Delete Selected"
3. Read confirmation dialog
4. Click "No" to cancel

**Expected Results:**
- ✅ Confirmation dialog appears with:
   - Number of files to delete
   - Space to free (GB)
   - "Files will be moved to Recycle Bin"
- ✅ Yes/No buttons
- ✅ Clicking "No" cancels operation
- ✅ No files deleted

**Pass/Fail:** ___________

**Dialog Text:** ___________________________________________

**Notes:** ___________________________________________

---

### TEST 14: Delete Selected - Execution ⭐⭐ CRITICAL

**Purpose:** Verify deletion actually works

**Steps:**
1. Apply "Keep Newest" strategy
2. Note number of files selected
3. Click "Delete Selected"
4. Click "Yes" in confirmation
5. Wait for completion
6. Check Recycle Bin

**Expected Results:**
- ✅ Progress bar appears during deletion
- ✅ Status updates: "Deleting duplicates... X/Y"
- ✅ Completion dialog shows:
   - Deleted: X files
   - Failed: 0 files
   - Space Freed: Y GB
- ✅ Files moved to Recycle Bin (NOT permanently deleted)
- ✅ Groups refresh automatically
- ✅ Groups with only 1 file left are removed
- ✅ Statistics update correctly
- ✅ History entry added

**Pass/Fail:** ___________

**Files Deleted:** ___________

**Space Freed:** ___________

**In Recycle Bin?** Yes / No

**Notes:** ___________________________________________

---

### TEST 15: Delete Selected - Group Cleanup

**Purpose:** Verify groups update correctly after deletion

**Steps:**
1. Before deletion, count total groups
2. Delete duplicates
3. Count groups after

**Expected Results:**
- ✅ Groups with all duplicates deleted are removed
- ✅ Groups with remaining duplicates stay
- ✅ Group counts update
- ✅ Summary statistics accurate

**Pass/Fail:** ___________

**Groups Before:** ___________

**Groups After:** ___________

**Notes:** ___________________________________________

---

### TEST 16: Move to Folder ⭐ CRITICAL

**Purpose:** Verify moving duplicates works

**Steps:**
1. Re-run detection (or restore from Recycle Bin)
2. Apply smart selection
3. Click "Move to Folder"
4. Create/select destination folder (e.g., C:\DuplicateReview)
5. Confirm

**Expected Results:**
- ✅ Folder browser dialog appears
- ✅ Can create new folder
- ✅ Progress bar during move
- ✅ Completion dialog shows:
   - Moved: X files
   - Failed: 0 files
   - Destination path
- ✅ Files actually moved to destination
- ✅ Name conflicts handled (adds counter)
- ✅ Detection re-runs automatically

**Pass/Fail:** ___________

**Files Moved:** ___________

**Destination:** ___________________________________________

**Notes:** ___________________________________________

---

### TEST 17: Export List ⭐ CRITICAL

**Purpose:** Verify CSV export works

**Steps:**
1. After detection, click "Export List"
2. Choose save location (e.g., Desktop\Duplicates_Test.csv)
3. Save file
4. Open CSV in Excel or text editor

**Expected Results:**
- ✅ Save dialog appears with default filename
- ✅ Default: "Duplicates_YYYYMMDD_HHMMSS.csv"
- ✅ File saves successfully
- ✅ CSV contains headers:
   - Group,Hash,FilePath,FileSize,Created,Modified,Selected,Recommended
- ✅ All duplicate groups included
- ✅ All files in each group listed
- ✅ Data accurate
- ✅ Opens correctly in Excel

**Pass/Fail:** ___________

**CSV Rows:** ___________

**Data Accurate?** Yes / No

**Notes:** ___________________________________________

---

### TEST 18: Statistics Accuracy

**Purpose:** Verify all statistics calculations are correct

**Steps:**
1. After detection, manually verify statistics
2. Compare with actual file counts/sizes

**Expected Results:**
- ✅ Duplicate Groups: Accurate count
- ✅ Duplicate Files: Accurate count (excludes kept copy)
- ✅ Wasted Space: Accurate calculation
- ✅ Selected to Delete: Updates correctly with selection
- ✅ Selected Space: Accurate calculation

**Pass/Fail:** ___________

**Manual Count Verification:**
- Groups: Expected _____ / Actual _____
- Files: Expected _____ / Actual _____
- Space: Expected _____ / Actual _____

**Notes:** ___________________________________________

---

### TEST 19: Large Scale Performance

**Purpose:** Test with realistic number of duplicates

**Steps:**
1. Use larger test dataset (100+ groups if possible)
2. Run detection
3. Apply smart selection
4. Scroll through groups

**Expected Results:**
- ✅ Detection completes in reasonable time
- ✅ UI remains responsive with many groups
- ✅ Scrolling smooth
- ✅ Selection updates without lag
- ✅ Delete/Move operations handle scale

**Pass/Fail:** ___________

**Dataset Size:**
- Files Scanned: _____
- Groups Found: _____
- Detection Time: _____ seconds

**UI Performance:** Excellent / Good / Acceptable / Poor

**Notes:** ___________________________________________

---

### TEST 20: Edge Case - No Duplicates

**Purpose:** Verify behavior when no duplicates found

**Steps:**
1. Create folder with unique files only
2. Run detection

**Expected Results:**
- ✅ Detection completes
- ✅ Message: "No duplicate files found!"
- ✅ Statistics show 0 groups
- ✅ Groups list empty
- ✅ Action buttons disabled appropriately

**Pass/Fail:** ___________

**Notes:** ___________________________________________

---

### TEST 21: Edge Case - Single Duplicate Group

**Purpose:** Verify behavior with minimal duplicates

**Steps:**
1. Create exactly 1 group of duplicates (2-3 files)
2. Run detection
3. Test all features

**Expected Results:**
- ✅ Shows 1 group correctly
- ✅ Smart selection works
- ✅ Delete works
- ✅ Export works

**Pass/Fail:** ___________

**Notes:** ___________________________________________

---

### TEST 22: Regression - Operations Tab

**Purpose:** Verify Operations tab still works

**Steps:**
1. Go to Operations tab
2. Verify "Detect Duplicates" button is GONE
3. Test other buttons (Dry Run, Live Move, Live Copy)

**Expected Results:**
- ✅ "Detect Duplicates" button removed
- ✅ Undo button still present
- ✅ Dry Run works
- ✅ Live Move works
- ✅ Live Copy works
- ✅ All existing features functional

**Pass/Fail:** ___________

**Notes:** ___________________________________________

---

### TEST 23: Regression - Other Tabs

**Purpose:** Verify all other tabs still work

**Steps:**
1. Test each tab: Queue, History, Statistics, Configuration, Help
2. Verify no visual breaks

**Expected Results:**
- ✅ Queue tab functional
- ✅ History tab functional
- ✅ Statistics tab functional
- ✅ Exceptions tab functional
- ✅ Configuration tab functional
- ✅ Help tab functional
- ✅ Version shows 1.2.0
- ✅ Changelog includes Build 1.2.0 entry

**Pass/Fail:** ___________

**Notes:** ___________________________________________

---

### TEST 24: Persistence - Settings

**Purpose:** Verify settings persist across sessions

**Steps:**
1. Select "Quick Scan" mode
2. Close application
3. Re-launch application
4. Go to Duplicates tab

**Expected Results:**
- ✅ Last scan mode remembered (Quick/Full)

**Pass/Fail:** ___________

**Notes:** ___________________________________________

---

### TEST 25: Error Handling - File Access

**Purpose:** Verify graceful handling of locked files

**Steps:**
1. Create duplicate files
2. Open one file in another program (lock it)
3. Try to delete that file

**Expected Results:**
- ✅ Deletion attempts file
- ✅ Fails gracefully on locked file
- ✅ Continues with other files
- ✅ Reports failure count
- ✅ No crash

**Pass/Fail:** ___________

**Notes:** ___________________________________________

---

## 📊 SUMMARY SCORECARD

### Critical Features (Must Pass All):
- [ ] TEST 1: Duplicates Tab Visibility
- [ ] TEST 2: Detect Duplicates (Full)
- [ ] TEST 3: Detect Duplicates (Quick)
- [ ] TEST 4: Expand/Collapse
- [ ] TEST 5: Group Display
- [ ] TEST 6: Keep Newest
- [ ] TEST 10: Manual Selection
- [ ] TEST 12: Delete Validation
- [ ] TEST 13: Delete Confirmation
- [ ] TEST 14: Delete Execution
- [ ] TEST 16: Move to Folder
- [ ] TEST 17: Export List
- [ ] TEST 22: Operations Regression

**Critical Pass Rate:** _____ / 13 (Need 13/13)

### Standard Features:
- [ ] TEST 7: Keep Oldest
- [ ] TEST 8: Keep Shortest Path
- [ ] TEST 9: Keep Longest Path
- [ ] TEST 11: Clear Selection
- [ ] TEST 15: Group Cleanup
- [ ] TEST 18: Statistics Accuracy
- [ ] TEST 19: Large Scale
- [ ] TEST 20: No Duplicates
- [ ] TEST 21: Single Group
- [ ] TEST 23: Other Tabs
- [ ] TEST 24: Persistence
- [ ] TEST 25: Error Handling

**Standard Pass Rate:** _____ / 12 (Need 10/12)

### Overall Score:
**Total Passed:** _____ / 25  
**Pass Percentage:** _____% (Need 92%+ to ship)

---

## 🚨 CRITICAL ISSUES LOG

| Test # | Issue | Severity | Status |
|--------|-------|----------|--------|
|        |       |          |        |
|        |       |          |        |
|        |       |          |        |

---

## ✅ APPROVAL CHECKLIST

**Build 1.2.0 is approved for release if:**

- [ ] All 13 critical tests pass
- [ ] At least 10/12 standard tests pass
- [ ] No severity 1 bugs
- [ ] Maximum 2 severity 2 bugs (with workarounds)
- [ ] UI is professional quality
- [ ] Performance acceptable
- [ ] No regressions
- [ ] Documentation complete

**Tester Signature:** _____________________

**Date:** _____________________

**Recommendation:** APPROVE / APPROVE WITH FIXES / REJECT

---

## 📝 NOTES SECTION

Use this space for general observations, suggestions, or additional findings:

_____________________________________________________________

_____________________________________________________________

_____________________________________________________________

_____________________________________________________________

_____________________________________________________________

---

**Test Plan Version:** 1.0  
**Created:** March 16, 2026  
**For Build:** v5.0 Build 1.2.0
