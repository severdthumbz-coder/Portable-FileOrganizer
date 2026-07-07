@echo off
echo ========================================
echo FileOrganizer Portable Build Script
echo ========================================
echo.

echo [1/3] Cleaning previous builds...
dotnet clean
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Clean failed!
    pause
    exit /b 1
)
echo.

echo [2/3] Restoring packages...
dotnet restore
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Restore failed!
    pause
    exit /b 1
)
echo.

echo [3/3] Building portable executable...
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
echo.

if %ERRORLEVEL% EQU 0 (
    echo ========================================
    echo SUCCESS! Portable .exe created:
    echo bin\Release\net9.0-windows\win-x64\publish\PortableFileOrganizer.exe
    echo.
    echo File size: ~200 MB (includes .NET runtime)
    echo No installation required on target PCs!
    echo ========================================
) else (
    echo ========================================
    echo BUILD FAILED! Check errors above.
    echo ========================================
)

echo.
pause
