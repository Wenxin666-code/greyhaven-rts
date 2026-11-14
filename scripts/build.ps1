# Compatibility entry for project notes written for the WinForms build.
$tool = Join-Path $PSScriptRoot '../legacy/windows/tools/build.ps1'
& (Join-Path $env:WINDIR 'System32/WindowsPowerShell/v1.0/powershell.exe') -NoProfile -ExecutionPolicy Bypass -File $tool @args
exit $LASTEXITCODE
