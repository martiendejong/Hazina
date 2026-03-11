@echo off
REM Wrapper to set CustomActionData environment variable and call PowerShell script
REM %1 contains the full path to the PowerShell script (passed from CustomActionData)

set CustomActionData=%*
powershell.exe -ExecutionPolicy Bypass -File "%~1"
exit /b %ERRORLEVEL%
