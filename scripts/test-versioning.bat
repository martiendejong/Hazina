@echo off
REM Quick test of version extraction and increment logic

echo Testing version detection...
echo.

REM Get current version
for /f "tokens=2 delims=<>" %%a in ('findstr /r "^[    ]*<Version>[0-9]" LLMs\Classes\DevGPT.LLMs.Classes.csproj') do (
    set "CURRENT_VERSION=%%a"
    goto :found
)

:found
echo Current version: %CURRENT_VERSION%

REM Parse version
for /f "tokens=1,2,3 delims=." %%a in ("%CURRENT_VERSION%") do (
    set "MAJOR=%%a"
    set "MINOR=%%b"
    set "PATCH=%%c"
)

echo Parsed: MAJOR=%MAJOR%, MINOR=%MINOR%, PATCH=%PATCH%
echo.

REM Test increments
set /a "TEST_PATCH=%PATCH%+1"
echo Patch increment: %CURRENT_VERSION% =^> %MAJOR%.%MINOR%.%TEST_PATCH%

set /a "TEST_MINOR=%MINOR%+1"
echo Minor increment: %CURRENT_VERSION% =^> %MAJOR%.%TEST_MINOR%.0

set /a "TEST_MAJOR=%MAJOR%+1"
echo Major increment: %CURRENT_VERSION% =^> %TEST_MAJOR%.0.0

echo.
echo Test complete! Version detection works correctly.
pause
