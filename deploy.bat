@echo off
REM ============================================================
REM Deploy script: copies the update package to the Astra Linux
REM update server (or a staging area for it).
REM Assumes SSH/SCP access to the update server.
REM ============================================================

setlocal

set SERVER=update-server.local
set REMOTE_PATH=/var/www/updates/installerupdater
set INSTALLER_DIR=%~dp0installer\output
set VERSION=1.0.0

echo ============================================
echo  Deploying InstallerUpdaterSetup v%VERSION%
echo ============================================

REM Check installer exists
if not exist "%INSTALLER_DIR%\InstallerUpdaterSetup-%VERSION%.exe" (
    echo ERROR: Installer not found at %INSTALLER_DIR%\InstallerUpdaterSetup-%VERSION%.exe
    echo Run build.bat and then iscc installer\InstallerUpdaterSetup.iss first.
    exit /b 1
)

REM Compute SHA-256 hash
echo Computing SHA-256 hash...
for /f "tokens=1" %%h in ('certutil -hashfile "%INSTALLER_DIR%\InstallerUpdaterSetup-%VERSION%.exe" SHA256 ^| findstr /v "hash"') do (
    set HASH=%%h
)

REM Generate manifest JSON
echo Generating update manifest...
(
echo {
echo   "version": "%VERSION%",
echo   "downloadUrl": "/updates/installerupdater/InstallerUpdaterSetup-%VERSION%.exe",
echo   "sha256Hash": "%HASH%",
echo   "fileSize": 0,
echo   "releaseNotes": "Release %VERSION%",
echo   "isMandatory": false,
echo   "minimumVersion": null,
echo   "publishedAt": "%DATE%T%TIME%"
echo }
) > "%INSTALLER_DIR%\manifest.json"

REM Upload via SCP
echo Uploading to %SERVER%...
scp "%INSTALLER_DIR%\InstallerUpdaterSetup-%VERSION%.exe" root@%SERVER%:%REMOTE_PATH%/
scp "%INSTALLER_DIR%\manifest.json" root@%SERVER%:%REMOTE_PATH%/latest.json

echo ============================================
echo  Deploy complete!
echo ============================================

endlocal
