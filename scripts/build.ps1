[CmdletBinding()]
param([switch]$Test)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$source = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object FullName)
$art = Join-Path $root 'art/source'
$images = @(Get-ChildItem -LiteralPath (Join-Path $art 'isometric/packed') -Filter '*.png')
foreach ($folder in @('v02/sprites','campaign/sprites')) {
    $images += Get-ChildItem -LiteralPath (Join-Path $art $folder) -Filter '*.png' | Where-Object BaseName -ne 'scout'
}
$resources = @($images | ForEach-Object { '/resource:' + $_.FullName + ',Greyhaven.Art.' + $_.Name })
$resources += '/resource:' + (Join-Path $art 'isometric/ground.png') + ',Greyhaven.Art.materials.png'
$options = @('/nologo','/optimize+','/platform:x64','/codepage:65001','/r:System.Windows.Forms.dll','/r:System.Drawing.dll',('/win32icon:' + (Join-Path $root 'art/command.ico')))
$exe = Join-Path $root '断线前沿.exe'
& $compiler @options /target:winexe "/out:$exe" @resources @source
if ($LASTEXITCODE) { throw '构建失败，请先关闭正在运行的游戏。' }
if ($Test) {
    foreach ($suite in @('CampaignChecks','WindowChecks')) {
        $check = Join-Path ([IO.Path]::GetTempPath()) ("greyhaven-{0}-{1}.exe" -f $suite,[guid]::NewGuid())
        try {
            & $compiler @options "/out:$check" "/main:$suite" @resources @source (Join-Path $root "tests/$suite.cs")
            if ($LASTEXITCODE) { throw "$suite 编译失败。" }
            & $check (Join-Path $root 'docs/桌面实机.png')
            if ($LASTEXITCODE) { throw "$suite 未通过。" }
        } finally {
            if (Test-Path -LiteralPath $check -PathType Leaf) { Remove-Item -LiteralPath $check }
        }
    }
}
Get-Item -LiteralPath $exe | Select-Object FullName,Length
