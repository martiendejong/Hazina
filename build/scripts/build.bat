@echo off

:: Save full path to logfile
set LOGFILE=%CD%\build_errors.log

:: Empty the log file
echo. > "%LOGFILE%"
echo dotnet build output >> "%LOGFILE%"

rem dotnet clean 1>nul 2>>"%LOGFILE%"
rem dotnet build 1>nul 2>>"%LOGFILE%"

dotnet build>>"%LOGFILE%"
