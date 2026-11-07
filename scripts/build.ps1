[CmdletBinding()]
param([switch]$Test,[switch]$MeasureFrames,[switch]$MeasureScroll,[switch]$ModelPreview,[switch]$SpacingPreview,[switch]$PresentationPreview,[switch]$TrafficPreview,[switch]$AnimationPreview,[switch]$MapPreview,[switch]$RiverPreview,[switch]$PathingPreview,[switch]$EconomyPreview,[switch]$ConstructionPreview,[switch]$InterfacePreview,[string]$DriveFrames,[string]$OreFrames)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$source = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object FullName)
$art = Join-Path $root 'art/source'
$mobile = @('squad','at','engineer','ifv','ifv-repair','tank','arty','mcv','miner','miner-full')
$images = @()
foreach ($key in @('hq','power','factory','barracks','radar','satellite','post','harbormaster','refinery','refining','residence','shop','municipal')) {
    foreach ($suffix in @('','-paint')) {
        $images += Get-Item -LiteralPath (Join-Path $art "buildings-v05/packed/iso-$key$suffix.png")
    }
}
# Explicit required resources: a missing atlas fails the build.
foreach ($key in @('hq','power','factory','barracks','radar','satellite','post','refinery')) {
    foreach ($action in @('build','collapse')) { foreach ($suffix in @('','-paint')) {
        $images += Get-Item -LiteralPath (Join-Path $art "animation-v06/packed/anim-$key-$action$suffix.png")
    } }
}
# Buildings and archived animation previews stay separate from unit art.
foreach ($key in $mobile) {
    if ($key -eq 'tank') { continue }
    $layers = if ($key -in @('squad','at','engineer')) { @('base','paint') } else { @('base','paint','wreck') }
    foreach ($layer in $layers) {
        $images += Get-Item -LiteralPath (Join-Path $art "units-v04/packed/$key-$layer.png")
    }
}
foreach ($layer in @('base','paint','wreck')) {
    $images += Get-Item -LiteralPath (Join-Path $art "tank-prototype/packed/tank-$layer.png")
}
$images += @(Get-ChildItem -LiteralPath (Join-Path $art 'console') -Filter '*.png' | Where-Object { $_.Name -ne 'portraits.png' })
$resources = @($images | ForEach-Object { '/resource:' + $_.FullName + ',Greyhaven.Art.' + $_.Name })
$resources += '/resource:' + (Join-Path $art 'isometric/ground.png') + ',Greyhaven.Art.materials.png'
$options = @('/nologo','/optimize+','/platform:x64','/codepage:65001',('/win32manifest:' + (Join-Path $root 'src/game.manifest')),'/r:System.Windows.Forms.dll','/r:System.Drawing.dll',('/win32icon:' + (Join-Path $root 'art/command.ico')))
$exe = Join-Path $root '断线前沿.exe'
& $compiler @options /target:winexe "/out:$exe" @resources @source
if ($LASTEXITCODE) { throw '构建失败，请先关闭正在运行的游戏。' }
if ($InterfacePreview -or $ConstructionPreview -or $EconomyPreview -or $Test -or $MeasureFrames -or $MeasureScroll -or $ModelPreview -or $SpacingPreview -or $PresentationPreview -or $TrafficPreview -or $AnimationPreview -or $MapPreview -or $RiverPreview -or $PathingPreview -or $DriveFrames -or $OreFrames) {
    foreach ($suite in @('ConstructionChecks','EconomyChecks','CampaignChecks','WindowChecks','PathingChecks')) {
        if ($InterfacePreview -and !$Test -and $suite -ne 'WindowChecks') { continue }
        if ($suite -eq 'ConstructionChecks' -and !$ConstructionPreview -and !$Test) { continue }
        if ($ConstructionPreview -and !$Test -and $suite -ne 'ConstructionChecks') { continue }
        if ($suite -eq 'EconomyChecks' -and !$EconomyPreview -and !$Test) { continue }
        if ($EconomyPreview -and !$Test -and $suite -ne 'EconomyChecks') { continue }
        if ($suite -eq 'PathingChecks' -and !$PathingPreview -and !$Test) { continue }
        if (!$Test -and $suite -eq 'WindowChecks' -and $PathingPreview -and !$RiverPreview -and !$MapPreview) { continue }
        if (!$Test -and $suite -eq 'CampaignChecks') { continue }
        $check = Join-Path ([IO.Path]::GetTempPath()) ("greyhaven-{0}-{1}.exe" -f $suite,[guid]::NewGuid())
        try {
            $checks = @((Join-Path $root "tests/$suite.cs"))
            if ($suite -eq 'WindowChecks') { $checks += Join-Path $root 'tests/TankPaintChecks.cs'; $checks += Join-Path $root 'tests/BattlePresentationChecks.cs'; $checks += Join-Path $root 'tests/BuildingArtChecks.cs'; $checks += Join-Path $root 'tests/AnimationChecks.cs'; $checks += Join-Path $root 'tests/RiverbendChecks.cs'; $checks += Join-Path $root 'tests/RiverbendPlayChecks.cs' }
            & $compiler @options "/out:$check" "/main:$suite" @resources @source @checks
            if ($LASTEXITCODE) { throw "$suite 编译失败。" }
            if ($suite -eq 'EconomyChecks' -or $suite -eq 'ConstructionChecks') { & $check (Join-Path $root 'docs'); if ($LASTEXITCODE) { throw 'EconomyChecks failed' }; continue }
            if (($PathingPreview -or $Test) -and $suite -eq 'PathingChecks') { & $check (Join-Path $root 'docs') }
            if ($LASTEXITCODE) { throw "$suite 未通过。" }
            if ($Test -and $suite -ne 'PathingChecks') { & $check (Join-Path $root 'docs/桌面实机.png') }
            elseif ($InterfacePreview -and $suite -eq 'WindowChecks') { & $check --controls (Join-Path $root 'docs/桌面实机.png') }
            if ($LASTEXITCODE) { throw "$suite 未通过。" }
            if ($ModelPreview -and $suite -eq 'WindowChecks') { & $check --model (Join-Path $root 'docs') }
            if ($AnimationPreview -and $suite -eq 'WindowChecks') { & $check --animation (Join-Path $root 'docs') }
            if ($RiverPreview -and $suite -eq 'WindowChecks') { & $check --river (Join-Path $root 'docs') }
            if ($LASTEXITCODE) { throw "$suite 未通过。" }
            if ($MapPreview -and $suite -eq 'WindowChecks') { & $check --map (Join-Path $root 'docs') }
            if ($LASTEXITCODE) { throw "$suite 未通过。" }
            if ($SpacingPreview -and $suite -eq 'WindowChecks') { & $check --spacing (Join-Path $root 'docs') }
            if ($LASTEXITCODE) { throw "$suite 未通过。" }
            if ($PresentationPreview -and $suite -eq 'WindowChecks') { & $check --presentation (Join-Path $root 'docs') }
            if ($LASTEXITCODE) { throw "$suite 未通过。" }
            if ($TrafficPreview -and $suite -eq 'WindowChecks') { & $check --traffic (Join-Path $root 'docs') }
            if ($LASTEXITCODE) { throw "$suite 未通过。" }
            if ($MeasureFrames -and $suite -eq 'WindowChecks') { & $check --profile }
            if ($LASTEXITCODE) { throw "$suite 未通过。" }
            if ($MeasureFrames -and $suite -eq 'WindowChecks') { & $check --window }
            if ($LASTEXITCODE) { throw "$suite 未通过。" }
            if ($MeasureScroll -and $suite -eq 'WindowChecks') { & $check --scroll }
            if ($LASTEXITCODE) { throw "$suite 未通过。" }
            if ($MeasureScroll -and $suite -eq 'WindowChecks') { & $check --scroll-window }
            if ($LASTEXITCODE) { throw "$suite 未通过。" }
            if ($DriveFrames -and $suite -eq 'WindowChecks') { & $check --drive $DriveFrames }
            if ($LASTEXITCODE) { throw "$suite 未通过。" }
            if ($OreFrames -and $suite -eq 'WindowChecks') { & $check --mine $OreFrames }
            if ($LASTEXITCODE) { throw "$suite 未通过。" }
        } finally {
            if (Test-Path -LiteralPath $check -PathType Leaf) { Remove-Item -LiteralPath $check }
        }
    }
}
Get-Item -LiteralPath $exe | Select-Object FullName,Length
