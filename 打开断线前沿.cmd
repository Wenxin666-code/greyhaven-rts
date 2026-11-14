@echo off
setlocal
set "GAME=%~dp0game\exports\windows\Greyhaven.exe"
if not exist "%GAME%" (
  echo Windows game export not found. See game\README.md.
  pause
  exit /b 1
)
start "" /d "%~dp0game\exports\windows" "%GAME%"
