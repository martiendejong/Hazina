@echo off
echo Fixing Hazina Orchestration file permissions...
echo.
echo This grants regular users the ability to edit appsettings.json
echo.

set INSTALLDIR=C:\Program Files (x86)\Hazina Orchestration

if not exist "%INSTALLDIR%\SetFilePermissions.ps1" (
    echo ERROR: SetFilePermissions.ps1 not found
    echo Make sure Hazina Orchestration is installed
    pause
    exit /b 1
)

powershell -Command "Start-Process powershell -ArgumentList '-ExecutionPolicy Bypass -File \"%INSTALLDIR%\SetFilePermissions.ps1\" -InstallDirParam \"%INSTALLDIR%\"' -Verb RunAs -Wait"

echo.
echo Verifying permissions...
powershell -Command "$acl = Get-Acl '%INSTALLDIR%\appsettings.json'; $users = $acl.Access | Where-Object { $_.IdentityReference -like '*Users*' -or $_.IdentityReference -like '*Gebruikers*' }; if ($users.FileSystemRights -like '*Modify*') { Write-Host 'SUCCESS: Users can now edit appsettings.json' -ForegroundColor Green } else { Write-Host 'FAILED: Permissions not set' -ForegroundColor Red; Write-Host 'Rights: ' $users.FileSystemRights }"

echo.
pause
