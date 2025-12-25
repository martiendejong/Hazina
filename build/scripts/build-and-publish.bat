@echo off
REM ==============================================================================
REM DevGPT NuGet Package Build and Publish Script (No Version Change)
REM ==============================================================================
REM Usage:
REM   build-and-publish.bat           - Builds and publishes with current version
REM   build-and-publish.bat --no-publish - Only builds, no publish
REM ==============================================================================

setlocal enabledelayedexpansion

REM Check if --no-publish flag is set
set "DO_PUBLISH=true"
if "%~1"=="--no-publish" set "DO_PUBLISH=false"

echo.
echo ========================================
echo DevGPT NuGet Package Builder
echo ========================================
echo Publish after build: %DO_PUBLISH%
echo.

REM Get current version from first csproj file
for /f "tokens=2 delims=<>" %%a in ('findstr /C:"<Version>" LLMs\Classes\DevGPT.LLMs.Classes.csproj') do (
    set "CURRENT_VERSION=%%a"
    goto :found_version
)

:found_version
if "%CURRENT_VERSION%"=="" (
    echo ERROR: Could not find current version in LLMs\Classes\DevGPT.LLMs.Classes.csproj
    exit /b 1
)

echo Current version: %CURRENT_VERSION%
echo.

REM Confirm with user
echo This will build all NuGet packages with version %CURRENT_VERSION%
set /p "CONFIRM=Continue? (y/n): "
if /i not "%CONFIRM%"=="y" (
    echo Cancelled by user.
    exit /b 0
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
    echo Build complete!
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
echo Version: %CURRENT_VERSION%
echo ========================================
echo.

endlocal
