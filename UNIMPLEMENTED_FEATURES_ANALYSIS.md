# Unimplemented Features Analysis - Build 1.0.15

**Analysis Date:** March 16, 2026  
**Current Build:** v5.0 Build 1.0.15

This document identifies features that exist in configuration/UI but are NOT actually implemented in the codebase.

---

## ❌ CRITICAL: Features That Don't Work

### 1. ScanMode (Auto/Normal/Fast/Turbo) - PARTIALLY IMPLEMENTED

**Status:** ⚠️ **ONLY works for Duplicate Detection, NOT for file scanning**

**What Exists:**
- ✅ ScanMode enum: Auto, Normal, Fast, Turbo
- ✅ UI dropdown in Configuration tab
- ✅ Config property: `ScanMode`
- ✅ Parameter passed to FileScanner.ScanDirectoryAsync()

**What's Missing:**
- ❌ **FileScanner.ScanDirectoryAsync() IGNORES the ScanMode parameter**
- ❌ All scans are single-threaded (no parallelization)
- ❌ Turbo/Fast modes don't actually scan faster

**Current Implementation:**
```csharp
// Services/FileScanner.cs line 63-111
public async Task<List<QueueEntry>> ScanDirectoryAsync(
    string sourcePath, 
    ScanMode scanMode,  // ← PARAMETER ACCEPTED BUT NEVER USED!
    ...)
{
    // Single-threaded foreach loop - ignores scanMode
    foreach (var file in files)
    {
        // Process one file at a time
    }
}
```

**Where It DOES Work:**
```csharp
// Services/DuplicateDetector.cs
// Turbo mode = 16 parallel threads for SHA256 hashing
```

**Impact:**
- User selects "Turbo" mode expecting faster scanning
- **NO DIFFERENCE** in scan speed (all modes are the same)
- **MISLEADING** - UI suggests it does something

**Recommendation:**
- **CRITICAL** - Implement parallel file scanning for Fast/Turbo modes
- OR remove the ScanMode option from scanning entirely (keep for duplicates only)

---

### 2. Semi-Exclude Exception Type - NOT IMPLEMENTED

**Status:** ❌ **COMPLETELY NON-FUNCTIONAL**

**What Exists:**
- ✅ ExceptionType enum has `Semi` value
- ✅ UI allows selecting "Semi-Exclude"
- ✅ Can add Semi-Exclude exceptions
- ✅ Shows "Semi-Exclude" in exceptions list

**What's Missing:**
- ❌ **ZERO implementation of Semi-Exclude logic**
- ❌ Semi exceptions are never checked
- ❌ No different behavior from Exclude

**Current Implementation:**
```csharp
// ViewModels/MainViewModel.cs line 1865
if (exception.Type == ExceptionType.Exclude)
{
    // Only checks for Exclude
    // Semi is NEVER checked!
}
```

**What Semi-Exclude Should Do (Per Roadmap):**
- Exclude the folder itself from being created
- BUT still organize the folder's contents into categories
- Example: "C:\Downloads" folder not created, but files go to Images/Documents/etc

**Impact:**
- Users select "Semi-Exclude" thinking it works differently
- **BEHAVES EXACTLY LIKE EXCLUDE** (does nothing)
- **MISLEADING** - appears functional but isn't

**Recommendation:**
- **HIGH PRIORITY** - Either implement Semi-Exclude OR remove it from UI
- Current state is deceptive

---

### 3. Date Organization - NOT IMPLEMENTED

**Status:** ❌ **COMPLETELY NON-FUNCTIONAL**

**What Exists:**
- ✅ Config property: `EnableDateOrganization` (boolean)
- ✅ Config property: `DateFormat` (string)
- ✅ UI checkbox in Configuration tab
- ✅ Date format options visible

**What's Missing:**
- ❌ **BuildDestinationPath() never checks EnableDateOrganization**
- ❌ No date-based folder creation
- ❌ DateFormat is stored but never used

**Current Implementation:**
```csharp
// Services/MoveEngine.cs BuildDestinationPath()
// Only handles: OrganizeByCategory, PreserveStructure, Hybrid
// NEVER checks _config.EnableDateOrganization
// NEVER uses file creation date or modification date
```

**What It Should Do:**
```
EnableDateOrganization = true
DateFormat = "Year\\Month (2024\\02)"

File: Photo.jpg (created Feb 2024)
Destination: DestRoot\Images\2024\02\Photo.jpg

OR with hybrid:
Destination: DestRoot\2024\02\Images\Photo.jpg
```

**Impact:**
- Users enable date organization expecting date-based folders
- **NOTHING HAPPENS** - files organized same as before
- **MISLEADING** - checkbox does nothing

**Recommendation:**
- **MEDIUM PRIORITY** - Implement date-based folder structure
- OR remove the checkbox and date format options entirely

---

### 4. Continue On Errors - NOT IMPLEMENTED

**Status:** ❌ **COMPLETELY NON-FUNCTIONAL**

**What Exists:**
- ✅ Config property: `ContinueOnErrors` (boolean, default=true)
- ✅ Saved to config file
- ✅ Loaded from config file

**What's Missing:**
- ❌ **ProcessQueueAsync NEVER checks this setting**
- ❌ Always continues on errors (can't stop)
- ❌ No way to halt operation on first error

**Current Implementation:**
```csharp
// Services/MoveEngine.cs ProcessQueueAsync()
foreach (var entry in queue)
{
    try { ... }
    catch (Exception ex)
    {
        entry.Status = $"Failed: {ex.Message}";
        result.FailedCount++;
        // ← ALWAYS continues to next file
        // ← NEVER checks _config.ContinueOnErrors
    }
    
    // Next file...
}
```

**What It Should Do:**
```csharp
catch (Exception ex)
{
    result.FailedCount++;
    
    if (!_config.ContinueOnErrors)
    {
        // Stop processing queue
        result.Status = "Stopped on error";
        break;
    }
}
```

**Impact:**
- Setting has NO EFFECT
- Operations always continue on errors
- Users can't choose to stop on first error

**Recommendation:**
- **LOW PRIORITY** - Current behavior (continue) is usually what users want
- BUT should either implement OR remove the config option

---

## ✅ FEATURES THAT DO WORK (Recently Fixed)

### 1. Retry Infrastructure - ✅ FULLY WIRED (Build 1.0.14)

**What Works:**
- ✅ RetryAttempts (default=3)
- ✅ RetryDelaySeconds (default=2)
- ✅ Automatic retry on verification failure
- ✅ Delete corrupted file before retry
- ✅ Retry tracking in verification stats

**Status:** FIXED in Build 1.0.14

---

### 2. Timestamp Preservation - ✅ FULLY IMPLEMENTED (Build 1.0.12)

**What Works:**
- ✅ PreserveTimestamps checkbox
- ✅ Preserves creation date, modification date, attributes
- ✅ Works with all 3 engines (CustomFast, TeraCopy, FastCopy)

**Status:** Working perfectly

---

### 3. Data Integrity Verification - ✅ FULLY IMPLEMENTED (Build 1.0.14/1.0.15)

**What Works:**
- ✅ 4 verification modes (None/SizeOnly/Smart/FullHash)
- ✅ SHA256 hashing
- ✅ Size verification
- ✅ Automatic retry
- ✅ Full transparency (Build 1.0.15)

**Status:** Working perfectly

---

## 📊 SUMMARY TABLE

| Feature | Config Exists | UI Exists | Implementation | Status | Priority |
|---------|--------------|-----------|----------------|--------|----------|
| **ScanMode (scanning)** | ✅ Yes | ✅ Yes | ❌ **NO** | ⚠️ Misleading | 🔴 HIGH |
| **ScanMode (duplicates)** | ✅ Yes | ✅ Yes | ✅ YES | ✅ Works | - |
| **Semi-Exclude** | ✅ Yes | ✅ Yes | ❌ **NO** | ❌ Broken | 🔴 HIGH |
| **Date Organization** | ✅ Yes | ✅ Yes | ❌ **NO** | ❌ Broken | 🟡 MEDIUM |
| **Continue On Errors** | ✅ Yes | ❌ No | ❌ **NO** | ⚠️ Ignored | 🟢 LOW |
| **Retry Infrastructure** | ✅ Yes | ❌ No | ✅ YES | ✅ Works | - |
| **Timestamps** | ✅ Yes | ✅ Yes | ✅ YES | ✅ Works | - |
| **Verification** | ✅ Yes | ✅ Yes | ✅ YES | ✅ Works | - |

---

## 🎯 RECOMMENDED ACTIONS

### Immediate (High Priority):

1. **ScanMode for File Scanning:**
   - **Option A:** Implement parallel scanning for Fast/Turbo modes
   - **Option B:** Remove ScanMode dropdown for scanning (keep for duplicates)
   - **Current:** Misleading - users think it works

2. **Semi-Exclude Exception Type:**
   - **Option A:** Implement Semi-Exclude logic (exclude folder but organize contents)
   - **Option B:** Remove "Semi" option from UI
   - **Current:** Completely non-functional but appears in UI

### Medium Priority:

3. **Date Organization:**
   - **Option A:** Implement date-based folder structure
   - **Option B:** Remove date organization checkbox from UI
   - **Current:** Does nothing despite being configurable

### Low Priority:

4. **Continue On Errors:**
   - **Option A:** Implement stop-on-error logic
   - **Option B:** Remove config property (always continue)
   - **Current:** No impact - always continues anyway

---

## 💡 USER IMPACT

**High Impact (Misleading):**
- ❌ **ScanMode** - Users select "Turbo" expecting fast scan, get same speed
- ❌ **Semi-Exclude** - Users add Semi exceptions, they do nothing
- ❌ **Date Organization** - Users enable date folders, files still organized by category

**Low Impact (Hidden):**
- ⚠️ **Continue On Errors** - No UI element, just unused config property

---

## ✅ BUILDS NEEDED TO FIX

### Build 1.0.16 - Remove Misleading Features
- Remove ScanMode from file scanning UI (keep for duplicates)
- Remove Semi-Exclude from exception type options
- Remove Date Organization checkbox
- **Result:** Honest UI showing only what works

### Build 1.1.0 - Implement Missing Features
- Implement parallel file scanning (Turbo mode)
- Implement Semi-Exclude logic
- Implement date-based organization
- Implement Continue On Errors
- **Result:** All promised features actually work

---

## 🚨 RECOMMENDATION

**CRITICAL:** Build 1.0.16 should REMOVE non-functional features from UI.

**Why:**
- Current state is **deceptive** to users
- Users think features work when they don't
- Better to remove than have broken features

**Then:** Build 1.1.0 can properly implement these features with full testing.

---

**Analysis Complete** - 4 features exist but don't work! 🚨
