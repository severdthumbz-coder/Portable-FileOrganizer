# QUICK START - FileOrganizer v5.0 Build 1.0.2

## ⚡ Fast Track to Building

### Method 1: Use the Batch File (Easiest!)
1. Double-click `build-portable.bat`
2. Wait ~30 seconds
3. Find your .exe in: `bin\Release\net9.0-windows\win-x64\publish\PortableFileOrganizer.exe`
4. Done! Copy it anywhere and run!

### Method 2: Manual Commands
```bash
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

## 🔧 Development Builds (Testing Only)

If you just want to test locally (not distribute):
```bash
dotnet build
dotnet run
```

This creates a small build that requires .NET 9.0 on your PC.

## 📦 What You Get

**Portable Build:**
- One .exe file (~200 MB)
- Runs on ANY Windows 10/11 64-bit PC
- No .NET installation needed
- No dependencies
- Just copy and run!

**Development Build:**
- Small .exe (~5 MB)
- Requires .NET 9.0 installed
- Fast compilation for testing

## ❌ Common Errors & Fixes

### Error: "Assets file doesn't have a target"
**Fix:** Run `dotnet restore` first, then build again

### Error: "Clean failed"
**Fix:** Close Visual Studio if open, delete `bin` and `obj` folders manually, then try again

### Error: "Project file is incomplete"
**Fix:** Make sure you extracted the complete ZIP file

## ✅ Verify Your Build

After building, check:
1. File exists: `bin\Release\net9.0-windows\win-x64\publish\PortableFileOrganizer.exe`
2. File size: ~200 MB (portable) or ~5 MB (framework-dependent)
3. Double-click to test - app should launch with splash screen

## 📁 Project Structure

```
FileOrganizer_v5.0_BUILD/
├── build-portable.bat          ← Double-click this!
├── FileOrganizer.csproj
├── MainWindow.xaml
├── App.xaml
├── Models/
├── ViewModels/
├── Services/
├── Themes/
└── PORTABLE_BUILD_GUIDE.md     ← Detailed instructions
```

## 🎯 Build Status

- ✅ Remove Exception button works
- ✅ Portable build configured
- ✅ All features functional
- ✅ Ready for distribution

## 🚀 Ready to Distribute?

1. Build using `build-portable.bat`
2. Find: `bin\Release\net9.0-windows\win-x64\publish\PortableFileOrganizer.exe`
3. Copy this ONE file
4. Share via email, USB, cloud, etc.
5. Users just double-click to run - no install needed!

---

**For detailed instructions, see `PORTABLE_BUILD_GUIDE.md`**
