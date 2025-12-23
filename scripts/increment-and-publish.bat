@echo off
REM ==============================================================================
REM DevGPT NuGet Package Version Increment and Publish Script
REM ==============================================================================
REM Usage:
REM   increment-and-publish.bat [patch|minor|major] [--no-publish]
REM
REM Examples:
REM   increment-and-publish.bat           - Increments patch version and publishes
REM   increment-and-publish.bat minor     - Increments minor version and publishes
REM   increment-and-publish.bat major     - Increments major version and publishes
REM   increment-and-publish.bat patch --no-publish - Only increments, no publish
REM ==============================================================================

setlocal enabledelayedexpansion

REM Default to patch increment if no argument provided
set "INCREMENT_TYPE=%~1"
if "%INCREMENT_TYPE%"=="" set "INCREMENT_TYPE=patch"

REM Check if --no-publish flag is set
set "DO_PUBLISH=true"
if "%~2"=="--no-publish" set "DO_PUBLISH=false"
if "%~1"=="--no-publish" set "DO_PUBLISH=false"

REM Validate increment type
if not "%INCREMENT_TYPE%"=="patch" if not "%INCREMENT_TYPE%"=="minor" if not "%INCREMENT_TYPE%"=="major" (
    echo ERROR: Invalid increment type '%INCREMENT_TYPE%'
    echo Valid options: patch, minor, major
    exit /b 1
)

echo.
echo ========================================
echo DevGPT NuGet Version Manager
echo ========================================
echo Increment type: %INCREMENT_TYPE%
echo Publish after increment: %DO_PUBLISH%
echo.

REM Get current version from first csproj file
REM Use more specific regex to avoid matching PackageReference Version attributes
for /f "tokens=2 delims=<>" %%a in ('findstr /r "^[    ]*<Version>[0-9]" LLMs\Classes\DevGPT.LLMs.Classes.csproj') do (
    set "CURRENT_VERSION=%%a"
    goto :found_version
)

:found_version
if "%CURRENT_VERSION%"=="" (
    echo ERROR: Could not find current version in DevGPT.Classes.csproj
    exit /b 1
)

REM Validate version format
echo %CURRENT_VERSION% | findstr /r "^[0-9][0-9]*\.[0-9][0-9]*\.[0-9][0-9]*$" >nul
if errorlevel 1 (
    echo ERROR: Invalid version format '%CURRENT_VERSION%'
    echo Version must be in format: major.minor.patch ^(e.g., 1.0.0^)
    echo Please fix the version in your .csproj files before running this script.
    exit /b 1
)

echo Current version: %CURRENT_VERSION%

REM Parse version components
for /f "tokens=1,2,3 delims=." %%a in ("%CURRENT_VERSION%") do (
    set "MAJOR=%%a"
    set "MINOR=%%b"
    set "PATCH=%%c"
)

REM Increment based on type
if "%INCREMENT_TYPE%"=="patch" (
    set /a "PATCH+=1"
) else if "%INCREMENT_TYPE%"=="minor" (
    set /a "MINOR+=1"
    set "PATCH=0"
) else if "%INCREMENT_TYPE%"=="major" (
    set /a "MAJOR+=1"
    set "MINOR=0"
    set "PATCH=0"
)

set "NEW_VERSION=!MAJOR!.!MINOR!.!PATCH!"
echo New version: !NEW_VERSION!
echo.

REM Confirm with user
echo This will update all NuGet packages to version !NEW_VERSION!
set /p "CONFIRM=Continue? (y/n): "
if /i not "%CONFIRM%"=="y" (
    echo Cancelled by user.
    exit /b 0
)

echo.
echo Updating version numbers...
powershell -ExecutionPolicy Bypass -Command "$targetVersion='!NEW_VERSION!'; & '%~dp0updateversions.ps1'"

if errorlevel 1 (
    echo ERROR: Failed to update versions
    exit /b 1
)

echo.
echo Building packages in Release mode...
dotnet clean
dotnet build -c Release

if errorlevel 1 (
    echo ERROR: Build failed
    exit /b 1
)

echo.
echo Creating NuGet packages...
dotnet pack -c Release --no-build

if errorlevel 1 (
    echo ERROR: Pack failed
    exit /b 1
)

if "%DO_PUBLISH%"=="false" (
    echo.
    echo ========================================
    echo Version increment complete!
    echo Packages created but not published.
    echo To publish manually, run: scripts\nuget-publish.bat
    echo ========================================
    exit /b 0
)

echo.
echo Publishing to NuGet...
call "%~dp0nuget-publish.bat"

if errorlevel 1 (
    echo ERROR: Publish failed
    exit /b 1
)

echo.
echo ========================================
echo SUCCESS! All packages published.
echo Version: %CURRENT_VERSION% =^> !NEW_VERSION!
echo ========================================
echo.
echo Don't forget to:
echo 1. Commit the version changes: git add . ^&^& git commit -m "Bump version to !NEW_VERSION!"
echo 2. Tag the release: git tag v!NEW_VERSION!
echo 3. Push: git push ^&^& git push --tags
echo.

endlocal
