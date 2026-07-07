@echo off
setlocal enabledelayedexpansion
echo ========================================
echo FileOrganizer Portable Build Script
echo ========================================
echo.

rem --- Read the version out of the .csproj so the exe name stays in sync ---
set "APP_VERSION="
for /f "tokens=3 delims=<>" %%V in ('findstr /i "<Version>" FileOrganizer.csproj') do set "APP_VERSION=%%V"
if not defined APP_VERSION set "APP_VERSION=0.0.0"
echo Detected version: v!APP_VERSION!
echo.

set "PUBLISH_DIR=bin\Release\net9.0-windows10.0.17763.0\win-x64\publish"
set "SRC_EXE=!PUBLISH_DIR!\PortableFileOrganizer.exe"
set "VERSIONED_EXE=!PUBLISH_DIR!\PortableFileOrganizer_v!APP_VERSION!.exe"

echo [1/3] Cleaning previous builds...
dotnet clean
if !ERRORLEVEL! NEQ 0 (
    echo ERROR: Clean failed!
    pause
    exit /b 1
)
echo.

echo [2/3] Restoring packages...
dotnet restore
if !ERRORLEVEL! NEQ 0 (
    echo ERROR: Restore failed!
    pause
    exit /b 1
)
echo.

echo [3/3] Building portable executable...
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
rem Capture the publish exit code IMMEDIATELY, before any other command
rem (including echo) can reset ERRORLEVEL.
set "PUBLISH_RESULT=!ERRORLEVEL!"
echo.

if !PUBLISH_RESULT! NEQ 0 (
    echo ========================================
    echo BUILD FAILED! Check errors above.
    echo Exit code: !PUBLISH_RESULT!
    echo ========================================
    echo.
    pause
    exit /b !PUBLISH_RESULT!
)

rem --- Publish succeeded. Verify the exe actually exists before celebrating. ---
if not exist "!SRC_EXE!" (
    echo ========================================
    echo BUILD FAILED! Publish reported success but the exe was not found:
    echo   !SRC_EXE!
    echo ========================================
    echo.
    pause
    exit /b 1
)

rem --- Create a versioned copy alongside the standard exe ---
copy /y "!SRC_EXE!" "!VERSIONED_EXE!" >nul
if !ERRORLEVEL! EQU 0 (
    set "FINAL_EXE=!VERSIONED_EXE!"
) else (
    echo WARNING: Could not create versioned copy; using default exe name.
    set "FINAL_EXE=!SRC_EXE!"
)

echo ========================================
echo SUCCESS! Portable .exe created:
echo   !FINAL_EXE!
echo.
echo File size: ~200 MB ^(includes .NET runtime^)
echo No installation required on target PCs!
echo ========================================
echo.
echo Press any key to open the publish folder...
pause >nul
start "" "!PUBLISH_DIR!"

endlocal
