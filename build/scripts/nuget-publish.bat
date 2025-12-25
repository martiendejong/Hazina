@echo off
REM ==============================================================================
REM DevGPT NuGet Package Publishing Script
REM ==============================================================================
REM This script publishes all NuGet packages to nuget.org
REM
REM Prerequisites:
REM   1. Set NUGET_API_KEY environment variable:
REM      setx NUGET_API_KEY "your-api-key-here"
REM   OR provide it as first argument:
REM      nuget-publish.bat your-api-key
REM
REM   2. Packages must be built in Release mode:
REM      dotnet pack -c Release
REM ==============================================================================

setlocal enabledelayedexpansion

REM Check for API key from argument or environment variable
set "API_KEY=%~1"
if "%API_KEY%"=="" set "API_KEY=%NUGET_API_KEY%"

if "%API_KEY%"=="" (
    echo ERROR: NuGet API key not found!
    echo.
    echo Please provide the API key in one of these ways:
    echo   1. Set environment variable: setx NUGET_API_KEY "your-api-key"
    echo   2. Pass as argument: nuget-publish.bat your-api-key
    echo.
    exit /b 1
)

echo.
echo ========================================
echo Publishing DevGPT Packages to NuGet
echo ========================================
echo.

REM Define package paths and names
set PKG_1_PATH=DevGPT.AgentFactory
set PKG_1_NAME=DevGPT.AgentFactory

set PKG_2_PATH=DevGPT.Generator
set PKG_2_NAME=DevGPT.Generator

set PKG_3_PATH=LLMs\Classes
set PKG_3_NAME=DevGPT.LLMs.Classes

set PKG_4_PATH=LLMs\Helpers
set PKG_4_NAME=DevGPT.LLMs.Helpers

set PKG_5_PATH=LLMs\Client
set PKG_5_NAME=DevGPT.LLMs.Client

set PKG_6_PATH=LLMs\OpenAI
set PKG_6_NAME=DevGPT.LLMs.OpenAI

set PKG_7_PATH=LLMs\Anthropic
set PKG_7_NAME=DevGPT.LLMs.Anthropic

set PKG_8_PATH=LLMs\HuggingFace
set PKG_8_NAME=DevGPT.LLMs.HuggingFace

set PKG_9_PATH=LLMs\ClientTools
set PKG_9_NAME=DevGPT.LLMClientTools

set PKG_10_PATH=Store\DocumentStore
set PKG_10_NAME=DevGPT.Store.DocumentStore

set PKG_11_PATH=Store\EmbeddingStore
set PKG_11_NAME=DevGPT.Store.EmbeddingStore

set PKG_12_PATH=App\ChatShared
set PKG_12_NAME=DevGPT.ChatShared

set PKG_13_PATH=LLMs\Gemini
set PKG_13_NAME=DevGPT.LLMs.Gemini

set PKG_14_PATH=LLMs\Mistral
set PKG_14_NAME=DevGPT.LLMs.Mistral

set PKG_15_PATH=DevGPT.DynamicAPI
set PKG_15_NAME=DevGPT.DynamicAPI

set "FAILED_PACKAGES="
set "SUCCESS_COUNT=0"
set "FAIL_COUNT=0"

REM Process each package
for /L %%i in (1,1,15) do (
    call :PublishPackage %%i
)

goto :Summary

:PublishPackage
setlocal enabledelayedexpansion
set INDEX=%1
set "PROJECT_PATH=!PKG_%INDEX%_PATH!"
set "PACKAGE_NAME=!PKG_%INDEX%_NAME!"

echo.
echo [!PACKAGE_NAME!] Searching for package...

set "NUPKG_FILE="
REM Find the .nupkg file (excluding .symbols.nupkg)
for /f "delims=" %%F in ('dir /b /s "!PROJECT_PATH!\bin\Release\!PACKAGE_NAME!.*.nupkg" 2^>nul ^| findstr /v "\.symbols\."') do (
    set "NUPKG_FILE=%%F"
)

if "!NUPKG_FILE!"=="" (
    echo [!PACKAGE_NAME!] WARNING: Package not found in !PROJECT_PATH!\bin\Release - skipping
    endlocal
    set /a "FAIL_COUNT+=1"
    set "FAILED_PACKAGES=!FAILED_PACKAGES! !PACKAGE_NAME!"
) else (
    echo [!PACKAGE_NAME!] Found: !NUPKG_FILE!
    echo [!PACKAGE_NAME!] Publishing...

    dotnet nuget push "!NUPKG_FILE!" --api-key "%API_KEY%" --source https://api.nuget.org/v3/index.json --skip-duplicate

    if errorlevel 1 (
        echo [!PACKAGE_NAME!] FAILED to publish
        endlocal
        set /a "FAIL_COUNT+=1"
        set "FAILED_PACKAGES=!FAILED_PACKAGES! !PACKAGE_NAME!"
    ) else (
        echo [!PACKAGE_NAME!] SUCCESS
        endlocal
        set /a "SUCCESS_COUNT+=1"
    )
)
goto :eof

:Summary

echo.
echo ========================================
echo Publishing Summary
echo ========================================
echo Successful: %SUCCESS_COUNT%
echo Failed: %FAIL_COUNT%

if not "%FAILED_PACKAGES%"=="" (
    echo.
    echo Failed packages:%FAILED_PACKAGES%
)

echo ========================================
echo.

if %FAIL_COUNT% gtr 0 (
    exit /b 1
)

endlocal
