@echo off
setlocal enabledelayedexpansion

echo Testing findstr pattern...
echo.

for /f "tokens=2 delims=<>" %%a in ('findstr /r "^[    ]*<Version>[0-9]" LLMs\Classes\DevGPT.LLMs.Classes.csproj') do (
    echo Found: %%a
    set "CURRENT_VERSION=%%a"
    goto :done
)

:done
echo.
echo CURRENT_VERSION=%CURRENT_VERSION%

endlocal
