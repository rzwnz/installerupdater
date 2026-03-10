@echo off
REM ============================================================
REM Build script for InstallerService + InstallerUpdater
REM Publishes self-contained Windows x64 binaries
REM ============================================================

setlocal enabledelayedexpansion

set SOLUTION_DIR=%~dp0
set PUBLISH_DIR=%SOLUTION_DIR%publish
set CONFIG=Release

echo ============================================
echo  InstallerUpdater Build Script
echo ============================================

REM Clean previous output
echo [1/5] Cleaning previous build output...
if exist "%PUBLISH_DIR%" rmdir /S /Q "%PUBLISH_DIR%"

REM Restore NuGet packages
echo [2/5] Restoring packages...
dotnet restore "%SOLUTION_DIR%InstallerUpdater.sln"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Package restore failed
    exit /b 1
)

REM Build the solution
echo [3/5] Building solution...
dotnet build "%SOLUTION_DIR%InstallerUpdater.sln" -c %CONFIG% --no-restore
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Build failed
    exit /b 1
)

REM Run tests
echo [4/5] Running tests...
dotnet test "%SOLUTION_DIR%src\InstallerService.Tests\InstallerService.Tests.csproj" -c %CONFIG% --no-build --verbosity normal
if %ERRORLEVEL% NEQ 0 (
    echo WARNING: Some tests failed
)

REM Publish both projects as self-contained
echo [5/5] Publishing...
dotnet publish "%SOLUTION_DIR%src\InstallerService\InstallerService.csproj" ^
    -c %CONFIG% ^
    -r win-x64 ^
    --self-contained true ^
    -o "%PUBLISH_DIR%\InstallerService" ^
    /p:PublishSingleFile=true ^
    /p:IncludeNativeLibrariesForSelfExtract=true
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: InstallerService publish failed
    exit /b 1
)

dotnet publish "%SOLUTION_DIR%src\InstallerUpdater\InstallerUpdater.csproj" ^
    -c %CONFIG% ^
    -r win-x64 ^
    --self-contained true ^
    -o "%PUBLISH_DIR%\InstallerUpdater" ^
    /p:PublishSingleFile=true ^
    /p:IncludeNativeLibrariesForSelfExtract=true
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: InstallerUpdater publish failed
    exit /b 1
)

echo ============================================
echo  Build complete!
echo  Output: %PUBLISH_DIR%
echo ============================================
echo.
echo Next steps:
echo   1. Run 'iscc installer\InstallerUpdaterSetup.iss' to build the installer
echo   2. The installer will be output to installer\output\

endlocal
