@echo off
setlocal
set "BUILD=%~dp0game\exports\windows"
set "RUNTIME=%BUILD%\data_Greyhaven.Godot_windows_x86_64"
for %%F in (
  "%BUILD%\Greyhaven.exe"
  "%BUILD%\Greyhaven.pck"
  "%RUNTIME%\Greyhaven.Godot.dll"
  "%RUNTIME%\Greyhaven.Core.dll"
  "%RUNTIME%\Greyhaven.Godot.deps.json"
  "%RUNTIME%\Greyhaven.Godot.runtimeconfig.json"
  "%RUNTIME%\hostfxr.dll"
  "%RUNTIME%\hostpolicy.dll"
  "%RUNTIME%\coreclr.dll"
) do (
  if not exist "%%~fF" (
    set "MISSING=%%~fF"
    goto :incomplete
  )
  if "%%~zF"=="0" (
    set "MISSING=%%~fF"
    goto :incomplete
  )
)
start "" /d "%BUILD%" "%BUILD%\Greyhaven.exe"
exit /b %ERRORLEVEL%

:incomplete
echo The Windows game export is incomplete. Missing or empty file:
echo "%MISSING%"
echo Restore or re-export the complete Windows build. See game\README.md.
pause
exit /b 1
