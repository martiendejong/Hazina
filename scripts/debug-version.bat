@echo off
echo Debugging version detection...
echo.

echo Testing findstr command:
findstr /r "^[    ]*<Version>[0-9]" LLMs\Classes\DevGPT.LLMs.Classes.csproj
echo.

echo Extracting version:
for /f "tokens=2 delims=<>" %%a in ('findstr /r "^[    ]*<Version>[0-9]" LLMs\Classes\DevGPT.LLMs.Classes.csproj') do (
    echo Token: %%a
)

pause
