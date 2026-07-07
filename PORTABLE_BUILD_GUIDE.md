# Portable Build Instructions - FileOrganizer v5.0 Build 1.0.2

## What is "Portable" and "Self-Contained"?

**Portable** = Single executable file that runs on any Windows machine without installation
**Self-Contained** = Includes .NET runtime, no dependencies needed on target machine

## Build Commands

### For Normal Development:
```bash
dotnet build
```
This builds the app for testing (requires .NET 9.0 on your machine).

### For Portable Distribution:

#### Option 1: Single-File Portable (Recommended)
This creates ONE executable file with everything bundled:

```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

**Output Location:**
```
bin\Release\net9.0-windows\win-x64\publish\PortableFileOrganizer.exe
```

**Result:** ~200 MB single .exe file (includes .NET runtime)

#### Option 2: Self-Contained Folder
Multiple files but still portable:

```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

**Output Location:**
```
bin\Release\net9.0-windows\win-x64\publish\
```

**Result:** Folder with .exe + DLLs (~150 files, ~200 MB total)

#### Option 3: Framework-Dependent (Smallest, Requires .NET)
Requires .NET 9.0 installed on target machine:

```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

**Result:** ~5 MB but needs .NET 9.0 on user's PC

## Project Settings (.csproj)

The following properties are configured in the project file:

```xml
<!-- Self-Contained Deployment Settings (applied during publish) -->
<PublishSingleFile>true</PublishSingleFile>
<PublishReadyToRun>true</PublishReadyToRun>
<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
<EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
```

**What these do:**
- `PublishSingleFile` = Bundle everything into one .exe
- `PublishReadyToRun` = Faster startup (pre-compiled)
- `IncludeNativeLibrariesForSelfExtract` = Include native DLLs
- `EnableCompressionInSingleFile` = Compress to reduce size

**Note:** `RuntimeIdentifier` and `SelfContained` are NOT in the .csproj file - they are specified in the publish command to allow normal builds to work without issues.

## Build Steps (Recommended)

### 1. Open PowerShell/Command Prompt

Navigate to project folder:
```bash
cd "C:\Users\ragin\Documents\App Development\FileOrganizer\Builds\v5.0 build 1.0.3 Final"
```

### 2. Clean Previous Builds (Optional)

```bash
dotnet clean
```

### 3. Restore NuGet Packages

```bash
dotnet restore
```

### 4. Publish Portable Version

```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

### 5. Find Your Portable .exe

```
bin\Release\net9.0-windows\win-x64\publish\PortableFileOrganizer.exe
```

### 6. Test It

Copy `PortableFileOrganizer.exe` to any folder and double-click to run!

## Platform Variations

### For 32-bit Windows:
```bash
dotnet publish -c Release -r win-x86 --self-contained true /p:PublishSingleFile=true
```

### For ARM64 Windows:
```bash
dotnet publish -c Release -r win-arm64 --self-contained true /p:PublishSingleFile=true
```

## Reducing File Size

### Option A: Trimming (Advanced)
Add to publish command:
```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=true
```

**Warning:** May break reflection-based features. Test thoroughly!

### Option B: Remove Unused Packages
The app uses Newtonsoft.Json which is included. If you can switch to System.Text.Json, it might be smaller.

## Distribution

### Single File (Recommended)
✅ Easy to share (email, USB, cloud)
✅ No installation needed
✅ Just run the .exe
❌ ~200 MB file size

### Installer (Alternative)
If 200 MB is too large, consider:
1. Build as framework-dependent (Option 3)
2. Create installer with .NET 9.0 check
3. Download .NET if not installed

## Troubleshooting

### "Error NETSDK1047: Assets file doesn't have a target for 'net9.0-windows/win-x64'"
**Cause:** Need to restore packages first
**Fix:** 
```bash
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

### "Error: The framework 'Microsoft.WindowsDesktop.App', version '9.0.0' was not found"
**Cause:** Built as framework-dependent, not self-contained
**Fix:** Use `--self-contained true` flag

### "File is too large (200+ MB)"
**Normal:** Self-contained .NET apps are large
**Options:**
- Accept the size (most users don't care)
- Use framework-dependent build (requires .NET on user's PC)
- Enable trimming (may break features)

### "App doesn't run on other computers"
**Check:**
1. Built with `--self-contained true`?
2. Used correct RuntimeIdentifier (win-x64 for 64-bit)?
3. Antivirus blocking the .exe? (common with self-extracting executables)

## Quick Reference

| Build Type | Command | Size | Requires .NET? | Portable? |
|------------|---------|------|----------------|-----------|
| **Development** | `dotnet build` | ~5 MB | ✅ Yes | ❌ No |
| **Single File** | `dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true` | ~200 MB | ❌ No | ✅ Yes |
| **Folder** | `dotnet publish -c Release -r win-x64 --self-contained true` | ~200 MB | ❌ No | ✅ Yes |
| **Framework** | `dotnet publish -c Release -r win-x64 --self-contained false` | ~5 MB | ✅ Yes | ❌ No |

## Recommended Workflow

1. **Development:** Normal build (`dotnet build`)
2. **Testing:** Framework-dependent (`dotnet publish -c Release -r win-x64 --self-contained false`)
3. **Distribution:** Self-contained single file (`dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true`)

## Complete Build Script

Copy this to a batch file (`build-portable.bat`):

```batch
@echo off
echo ========================================
echo FileOrganizer Portable Build Script
echo ========================================
echo.

echo [1/3] Cleaning previous builds...
dotnet clean
echo.

echo [2/3] Restoring packages...
dotnet restore
echo.

echo [3/3] Building portable executable...
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
echo.

if %ERRORLEVEL% EQU 0 (
    echo ========================================
    echo SUCCESS! Portable .exe created:
    echo bin\Release\net9.0-windows\win-x64\publish\PortableFileOrganizer.exe
    echo ========================================
) else (
    echo ========================================
    echo BUILD FAILED! Check errors above.
    echo ========================================
)

pause
```

Save this file in your project folder and double-click to build!

---

**Your app is configured for portable builds - just use the correct publish command!**
