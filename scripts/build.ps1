[CmdletBinding()]
param([switch]$InfantryVerify,[switch]$InfantryPreview,[switch]$VeterancyVerify,[switch]$VeterancyPreview,[switch]$PerformanceIFVVerify,[switch]$PerformanceIFVPreview,[switch]$NativePerformance,[int]$PerformanceWarmup=580,[switch]$BalanceVerify,[switch]$BalancePreview,[switch]$StoryVerify,[switch]$StoryPreview,[switch]$RelayVerify,[switch]$RelayPreview,[switch]$MinerVerify,[switch]$PerformanceVerify,[switch]$TerritoryVerify,[switch]$DefenseVerify,[switch]$DefensePreview,[switch]$HaulVerify,[switch]$HaulPreview,[switch]$TempoVerify,[switch]$TempoPreview,[switch]$ControlsVerify,[switch]$ControlsPreview,[switch]$IslandPreview,[switch]$AiVerify,[switch]$ProductionVerify,[switch]$BlastVerify,[switch]$BlastPreview,[switch]$StructurePreview,[switch]$MotionPreview,[switch]$BuildingPreview,[switch]$ScrollVerify,[switch]$ScrollPreview,[switch]$QueuePreview,[switch]$Test,[switch]$MeasureFrames,[switch]$MeasureScroll,[switch]$ModelPreview,[switch]$SpacingPreview,[switch]$PresentationPreview,[switch]$TrafficPreview,[switch]$AnimationPreview,[switch]$MapPreview,[switch]$RiverPreview,[switch]$PathingPreview,[switch]$EconomyPreview,[switch]$ConstructionPreview,[switch]$InterfacePreview,[string]$DriveFrames,[string]$OreFrames)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$source = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object FullName)
$art = Join-Path $root 'art/source'
$mobile = @('squad','at','engineer','ifv','ifv-repair','tank','arty','mcv','miner','miner-full')
$images = @()
foreach ($key in @('harbormaster')) {
    foreach ($suffix in @('','-paint')) {
        $images += Get-Item -LiteralPath (Join-Path $art "buildings-v05/packed/iso-$key$suffix.png")
    }
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
$infantryPack=Get-Item -LiteralPath (Join-Path $art "infantry-v12/packed/infantry-v12.pak")
$resources += "/resource:" + $infantryPack.FullName + ",Greyhaven.Art.infantry-v12.pak"
$relayPack=Get-Item -LiteralPath (Join-Path $art 'relay-v01/packed/relay-v01.pak')
$resources += '/resource:' + $relayPack.FullName + ',Greyhaven.Art.relay-v01.pak'
$pack=Get-Item -LiteralPath (Join-Path $art 'buildings-v09/packed/buildings-v09.pak')
$resources += '/resource:' + $pack.FullName + ',Greyhaven.Art.buildings-v09.pak'
$structurePack=Get-Item -LiteralPath (Join-Path $art 'buildings-v10/packed/structures-v10.pak')
$resources += '/resource:' + $structurePack.FullName + ',Greyhaven.Art.structures-v10.pak'
$gaitPack=Get-Item -LiteralPath (Join-Path $art 'unit-motion-v09/packed/gait-v09.pak')
$resources += '/resource:' + $gaitPack.FullName + ',Greyhaven.Art.gait-v09.pak'
foreach($effect in @('flame','soot','blast')) {
 $effectFile=Join-Path $art ("buildings-v10/materials/hazard-"+$effect+"-v10.png")
 if(Test-Path -LiteralPath $effectFile){$resources += '/resource:' + $effectFile + ',Greyhaven.Art.hazard-' + $effect + '-v10.png'}
}
foreach($effect in @('blast-cloud-source','blast-cloud','debris-base','debris-paint')) {
 $effectFile=Join-Path $art ("blast-v11/"+$effect+"-v11.png")
 if(Test-Path -LiteralPath $effectFile){$resources += '/resource:' + $effectFile + ',Greyhaven.Art.' + $effect + '-v11.png'}
}
$resources += '/resource:' + (Join-Path $art 'isometric/ground.png') + ',Greyhaven.Art.materials.png'
$options = @('/nologo','/optimize+','/platform:x64','/codepage:65001',('/win32manifest:' + (Join-Path $root 'src/game.manifest')),'/r:System.Windows.Forms.dll','/r:System.Drawing.dll',('/win32icon:' + (Join-Path $root 'art/command.ico')))
if ($InfantryVerify -or $InfantryPreview -or $Test) {
 foreach($main in @($(if($InfantryVerify -or $Test){'InfantryChecks'}),$(if($InfantryPreview){'InfantryInterfaceChecks'}))) {
  if(-not $main){continue}
  $check=Join-Path ([IO.Path]::GetTempPath()) ('greyhaven-infantry-'+[guid]::NewGuid()+'.exe')
  try {
   & $compiler @options "/out:$check" "/main:$main" @resources @source (Join-Path $root ('tests/'+$main+'.cs'))
   if($LASTEXITCODE){throw '步兵专项编译失败'}
   & $check (Join-Path $root 'docs/infantry-20261110')
   if($LASTEXITCODE){throw '步兵专项验证失败'}
  } finally {if(Test-Path -LiteralPath $check){Remove-Item -LiteralPath $check}}
 }
}
$exe = Join-Path $root '断线前沿.exe'
& $compiler @options /target:winexe "/out:$exe" @resources @source
if ($LASTEXITCODE) { throw '构建失败，请先关闭正在运行的游戏。' }
if ($VeterancyVerify -or $VeterancyPreview -or $Test) {
 $output=Join-Path $root 'docs/veterancy-20261110'
 [IO.Directory]::CreateDirectory($output)|Out-Null
 foreach($checkMain in @($(if($VeterancyVerify -or $Test){'AdjacencyVeterancyChecks'}),$(if($VeterancyPreview){'AdjacencyVeterancyInterfaceChecks'}))){
  if(-not $checkMain){continue}
  $check=Join-Path ([IO.Path]::GetTempPath()) ('greyhaven-veterancy-'+[guid]::NewGuid()+'.exe')
  $checkResources=@();if($checkMain -eq 'AdjacencyVeterancyInterfaceChecks'){$checkResources=$resources}
  try{
   & $compiler @options "/out:$check" "/main:$checkMain" @checkResources @source (Join-Path $root ('tests/'+$checkMain+'.cs'))
   if($LASTEXITCODE){throw '晋升与扩建检查编译失败'}
   & $check $output
   if($LASTEXITCODE){throw '晋升与扩建检查未通过'}
  }finally{if(Test-Path -LiteralPath $check){Remove-Item -LiteralPath $check}}
 }
}
if ($PerformanceIFVVerify -or $PerformanceIFVPreview -or $NativePerformance) {
 $output=Join-Path $root 'docs/performance-20261110'
 [IO.Directory]::CreateDirectory($output)|Out-Null
 foreach($checkMain in @($(if($PerformanceIFVVerify){'PerformanceAndIFVChecks'}),$(if($PerformanceIFVPreview){'PerformanceInterfaceChecks'}),$(if($NativePerformance){'PerformanceNativeChecks'}))){
  if(-not $checkMain){continue}
  $check=Join-Path ([IO.Path]::GetTempPath()) ('greyhaven-performance-'+[guid]::NewGuid()+'.exe')
  $testFiles=@((Join-Path $root ('tests/'+$checkMain+'.cs')))
  if($checkMain -eq 'PerformanceAndIFVChecks'){$testFiles+=Join-Path $root 'tests/ThirdMissionChecks.cs'}
  if($checkMain -eq 'PerformanceInterfaceChecks'){$testFiles+=Join-Path $root 'tests/ParallelProductionChecks.cs'}
  if($checkMain -eq 'PerformanceNativeChecks'){$testFiles+=Join-Path $root 'tests/AiStandoffSimulation.cs'}
  try{
   & $compiler @options "/out:$check" "/main:$checkMain" @resources @source @testFiles
   if($LASTEXITCODE){throw '性能与载员检查编译失败'}
   & $check (Join-Path $output $checkMain) $PerformanceWarmup
   if($LASTEXITCODE){throw '性能与载员检查未通过'}
  }finally{if(Test-Path -LiteralPath $check){Remove-Item -LiteralPath $check}}
 }
}
if ($BalanceVerify -or $Test) {
 $check=Join-Path ([IO.Path]::GetTempPath()) ("greyhaven-balance-{0}.exe" -f [guid]::NewGuid())
 try {
  & $compiler @options "/out:$check" /main:BalanceChecks @source (Join-Path $root 'tests/BalanceChecks.cs')
  if($LASTEXITCODE){throw '数值检查编译失败'}
  & $check (Join-Path $root 'docs/balance-20261109')
  if($LASTEXITCODE){throw '数值检查未通过'}
 }finally{if(Test-Path -LiteralPath $check){Remove-Item -LiteralPath $check}}
}
if ($BalancePreview) {
 $check=Join-Path ([IO.Path]::GetTempPath()) ("greyhaven-balance-ui-{0}.exe" -f [guid]::NewGuid())
 try {
  & $compiler @options "/out:$check" /main:BalanceInterfaceChecks @resources @source (Join-Path $root 'tests/BalanceInterfaceChecks.cs')
  if($LASTEXITCODE){throw '数值界面编译失败'}
  & $check (Join-Path $root 'docs/balance-20261109')
  if($LASTEXITCODE){throw '数值界面检查未通过'}
 }finally{if(Test-Path -LiteralPath $check){Remove-Item -LiteralPath $check}}
}
if ($StoryVerify -or $Test) {
 $storyCheck=Join-Path ([IO.Path]::GetTempPath()) ("greyhaven-story-{0}.exe" -f [guid]::NewGuid())
 try {
  & $compiler @options "/out:$storyCheck" /main:CampaignStoryChecks @source (Join-Path $root 'tests/CampaignStoryChecks.cs') (Join-Path $root 'tests/TerritorialAiChecks.cs')
  if($LASTEXITCODE){throw '战役行为检查编译失败'}
  & $storyCheck
  if($LASTEXITCODE){throw '战役行为检查未通过'}
 }finally{if(Test-Path -LiteralPath $storyCheck){Remove-Item -LiteralPath $storyCheck}}
}
if ($StoryPreview) {
 $storyCheck=Join-Path ([IO.Path]::GetTempPath()) ("greyhaven-story-ui-{0}.exe" -f [guid]::NewGuid())
 try {
  & $compiler @options "/out:$storyCheck" /main:CampaignStoryInterfaceChecks @resources @source (Join-Path $root 'tests/CampaignStoryInterfaceChecks.cs')
  if($LASTEXITCODE){throw '战役界面检查编译失败'}
  & $storyCheck (Join-Path $root 'docs/story-ai-20261109')
  if($LASTEXITCODE){throw '战役界面检查未通过'}
 }finally{if(Test-Path -LiteralPath $storyCheck){Remove-Item -LiteralPath $storyCheck}}
}
if ($RelayVerify -or $Test) {
 $relayCheck=Join-Path ([IO.Path]::GetTempPath()) ("greyhaven-relay-{0}.exe" -f [guid]::NewGuid())
 try {
  & $compiler @options "/out:$relayCheck" /main:RelayMissionChecks @source (Join-Path $root 'tests/RelayMissionChecks.cs') (Join-Path $root 'tests/RelaySimulationChecks.cs')
  if($LASTEXITCODE){throw '第四关检查编译失败'}
  & $relayCheck
  if($LASTEXITCODE){throw '第四关检查未通过'}
 }finally{if(Test-Path -LiteralPath $relayCheck){Remove-Item -LiteralPath $relayCheck}}
}
if ($RelayPreview) {
 $relayCheck=Join-Path ([IO.Path]::GetTempPath()) ("greyhaven-relay-ui-{0}.exe" -f [guid]::NewGuid())
 try {
  & $compiler @options "/out:$relayCheck" /main:RelayInterfaceChecks @resources @source (Join-Path $root 'tests/RelayInterfaceChecks.cs')
  if($LASTEXITCODE){throw '第四关预览编译失败'}
  & $relayCheck (Join-Path $root 'docs/relay-20261109')
  if($LASTEXITCODE){throw '第四关界面检查未通过'}
 }finally{if(Test-Path -LiteralPath $relayCheck){Remove-Item -LiteralPath $relayCheck}}
}
if ($MinerVerify -or $Test) {
 $minerCheck=Join-Path ([IO.Path]::GetTempPath()) ("greyhaven-miner-{0}.exe" -f [guid]::NewGuid())
 try {
  & $compiler @options "/out:$minerCheck" /main:MinerDeliveryChecks @source (Join-Path $root 'tests/MinerDeliveryChecks.cs')
  if($LASTEXITCODE){throw '矿车返厂检查编译失败'}
  & $minerCheck
  if($LASTEXITCODE){throw '矿车返厂检查未通过'}
 }finally{if(Test-Path -LiteralPath $minerCheck){Remove-Item -LiteralPath $minerCheck}}
}
if ($PerformanceVerify -or $Test) {
 $performanceCheck=Join-Path ([IO.Path]::GetTempPath()) ("greyhaven-performance-{0}.exe" -f [guid]::NewGuid())
 try {
  & $compiler @options "/out:$performanceCheck" /main:PerformanceChecks @source (Join-Path $root 'tests/PerformanceChecks.cs')
  if($LASTEXITCODE){throw '性能等价检查编译失败'}
  & $performanceCheck
  if($LASTEXITCODE){throw '性能等价检查未通过'}
 }finally{if(Test-Path -LiteralPath $performanceCheck){Remove-Item -LiteralPath $performanceCheck}}
}
if ($TerritoryVerify -or $Test) {
 $territoryCheck=Join-Path ([IO.Path]::GetTempPath()) ("greyhaven-territory-{0}.exe" -f [guid]::NewGuid())
 try {
  & $compiler @options "/out:$territoryCheck" /main:TerritorialAiChecks @resources @source (Join-Path $root 'tests/TerritorialAiChecks.cs')
  if($LASTEXITCODE){throw '基地守备与出击检查编译失败'}
  & $territoryCheck
  if($LASTEXITCODE){throw '基地守备与出击检查未通过'}
 }finally{if(Test-Path -LiteralPath $territoryCheck){Remove-Item -LiteralPath $territoryCheck}}
}
if ($DefenseVerify -or $DefensePreview -or $Test) {
 $defenseCheck=Join-Path ([IO.Path]::GetTempPath()) ("greyhaven-defense-{0}.exe" -f [guid]::NewGuid())
 try {
  & $compiler @options "/out:$defenseCheck" /main:DefenseChecks @resources @source (Join-Path $root 'tests/DefenseChecks.cs')
  if($LASTEXITCODE){throw '防御检查编译失败'}
  if($DefensePreview){& $defenseCheck (Join-Path $root 'docs/defense-20261108')}else{& $defenseCheck}
  if($LASTEXITCODE){throw '防御检查未通过'}
 }finally{if(Test-Path -LiteralPath $defenseCheck){Remove-Item -LiteralPath $defenseCheck}}
}
if ($HaulVerify -or $HaulPreview -or $Test) {
 $haulCheck=Join-Path ([IO.Path]::GetTempPath()) ("greyhaven-haul-{0}.exe" -f [guid]::NewGuid())
 try {
  & $compiler @options "/out:$haulCheck" /main:HaulAndBoundsChecks @resources @source (Join-Path $root 'tests/HaulAndBoundsChecks.cs')
  if($LASTEXITCODE){throw '矿车与边界检查编译失败'}
  if($HaulPreview){& $haulCheck (Join-Path $root 'docs/haul-map-20261108')}else{& $haulCheck}
  if($LASTEXITCODE){throw '矿车与边界检查未通过'}
 }finally{if(Test-Path -LiteralPath $haulCheck){Remove-Item -LiteralPath $haulCheck}}
}
if ($TempoVerify -or $TempoPreview -or $Test) {
 $tempoCheck=Join-Path ([IO.Path]::GetTempPath()) ("greyhaven-tempo-{0}.exe" -f [guid]::NewGuid())
 try {
  & $compiler @options "/out:$tempoCheck" /main:MatchTempoChecks @resources @source (Join-Path $root 'tests/MatchTempoChecks.cs')
  if($LASTEXITCODE){throw '对局节奏检查编译失败'}
  if($TempoPreview){& $tempoCheck (Join-Path $root 'docs/tempo-20261108')}else{& $tempoCheck}
  if($LASTEXITCODE){throw '对局节奏检查未通过'}
 }finally{if(Test-Path -LiteralPath $tempoCheck){Remove-Item -LiteralPath $tempoCheck}}
}
if ($ControlsPreview -or $ControlsVerify -or $Test) {
 $controlsCheck=Join-Path ([IO.Path]::GetTempPath()) ("greyhaven-controls-{0}.exe" -f [guid]::NewGuid())
 try {
  & $compiler @options "/out:$controlsCheck" /main:OriginalControlChecks @resources @source (Join-Path $root 'tests/OriginalControlChecks.cs')
  if($LASTEXITCODE){throw '键鼠与界面核对编译失败'}
  $controlsOutput=if($ControlsPreview){Join-Path $root 'docs/controls-20261108'}else{Join-Path $root '.cache/controls-checks-media'}
  [IO.Directory]::CreateDirectory($controlsOutput) | Out-Null
  & $controlsCheck (Join-Path $root 'tests/fixtures/yr-default-hotkeys.csv') $controlsOutput
  if($LASTEXITCODE){throw '键鼠与界面核对未通过'}
 } finally {if(Test-Path -LiteralPath $controlsCheck){Remove-Item -LiteralPath $controlsCheck}}
}
if ($IslandPreview -or $AiVerify -or $Test) {
 $islandCheck=Join-Path ([IO.Path]::GetTempPath()) ("greyhaven-island-{0}.exe" -f [guid]::NewGuid())
 try {
  & $compiler @options "/out:$islandCheck" /main:ThirdMissionChecks @resources @source (Join-Path $root 'tests/ThirdMissionChecks.cs')
  if($LASTEXITCODE){throw '第三关与 AI 检查编译失败'}
  if($IslandPreview){& $islandCheck (Join-Path $root 'docs')}else{& $islandCheck}
  if($LASTEXITCODE){throw '第三关与 AI 检查未通过'}
 } finally {if(Test-Path -LiteralPath $islandCheck){Remove-Item -LiteralPath $islandCheck}}
}
if ($ProductionVerify -or $Test) {
 $productionCheck=Join-Path ([IO.Path]::GetTempPath()) ("greyhaven-production-{0}.exe" -f [guid]::NewGuid())
 try {
  & $compiler @options "/out:$productionCheck" /main:ParallelProductionChecks @resources @source (Join-Path $root 'tests/ParallelProductionChecks.cs')
  if($LASTEXITCODE){throw '并行生产检查编译失败'}
  & $productionCheck (Join-Path $root 'docs')
  if($LASTEXITCODE){throw '并行生产检查未通过'}
 } finally {if(Test-Path -LiteralPath $productionCheck){Remove-Item -LiteralPath $productionCheck}}
}
if ($BlastPreview -or $BlastVerify -or $Test) {
 $blastCheck=Join-Path ([IO.Path]::GetTempPath()) ("greyhaven-blast-{0}.exe" -f [guid]::NewGuid())
 try {
  & $compiler @options "/out:$blastCheck" /main:BlastEffectChecks @resources @source (Join-Path $root 'tests/BuildingV08Checks.cs') (Join-Path $root 'tests/StructuralAnimationChecks.cs') (Join-Path $root 'tests/BlastEffectChecks.cs')
  if($LASTEXITCODE){throw '弹片特效检查编译失败'}
  if($BlastPreview){& $blastCheck (Join-Path $root 'docs')}else{& $blastCheck}
  if($LASTEXITCODE){throw '弹片特效检查未通过'}
 } finally {if(Test-Path -LiteralPath $blastCheck){Remove-Item -LiteralPath $blastCheck}}
}
if ($StructurePreview -or $Test) {
 $structureCheck=Join-Path ([IO.Path]::GetTempPath()) ("greyhaven-structure-{0}.exe" -f [guid]::NewGuid())
 try {
  & $compiler @options "/out:$structureCheck" /main:StructuralAnimationChecks @resources @source (Join-Path $root 'tests/BuildingV08Checks.cs') (Join-Path $root 'tests/StructuralAnimationChecks.cs')
  if($LASTEXITCODE){throw '结构动画检查编译失败'}
  if($StructurePreview){& $structureCheck (Join-Path $root 'docs')}else{& $structureCheck}
  if($LASTEXITCODE){throw '结构动画检查未通过'}
 } finally {if(Test-Path -LiteralPath $structureCheck){Remove-Item -LiteralPath $structureCheck}}
}
if ($MotionPreview -or $Test) {
 $motionCheck=Join-Path ([IO.Path]::GetTempPath()) ("greyhaven-motion-{0}.exe" -f [guid]::NewGuid())
 try {
  & $compiler @options "/out:$motionCheck" /main:FacilityAnimationChecks @resources @source (Join-Path $root 'tests/BuildingV08Checks.cs') (Join-Path $root 'tests/FacilityAnimationChecks.cs')
  if($LASTEXITCODE){throw '工作动画检查编译失败'}
  if($MotionPreview){& $motionCheck (Join-Path $root 'docs')}else{& $motionCheck}
  if($LASTEXITCODE){throw '工作动画检查未通过'}
 } finally {if(Test-Path -LiteralPath $motionCheck){Remove-Item -LiteralPath $motionCheck}}
}
if ($BuildingPreview -or $Test) {
    $buildingCheck = Join-Path ([IO.Path]::GetTempPath()) ("greyhaven-buildings-{0}.exe" -f [guid]::NewGuid())
    try {
        & $compiler @options "/out:$buildingCheck" /main:BuildingV08Checks @resources @source (Join-Path $root 'tests/BuildingV08Checks.cs')
        if ($LASTEXITCODE) { throw '建筑动画检查编译失败。' }
        if ($BuildingPreview) { & $buildingCheck (Join-Path $root 'docs') --video } else { & $buildingCheck }
        if ($LASTEXITCODE) { throw '建筑模型与动画检查未通过。' }
    } finally { if (Test-Path -LiteralPath $buildingCheck) { Remove-Item -LiteralPath $buildingCheck } }
}
if ($ScrollPreview -or $ScrollVerify -or $Test) {
    $scrollCheck = Join-Path ([IO.Path]::GetTempPath()) ("greyhaven-scroll-{0}.exe" -f [guid]::NewGuid())
    try {
        & $compiler @options "/out:$scrollCheck" /main:ScrollChecks @resources @source (Join-Path $root 'tests/ScrollChecks.cs')
        if ($LASTEXITCODE) { throw '滚屏检查编译失败。' }
        if ($ScrollPreview) { & $scrollCheck } else { & $scrollCheck --verify-only }
        if ($LASTEXITCODE) { throw '滚屏检查未通过。' }
    } finally { if (Test-Path -LiteralPath $scrollCheck) { Remove-Item -LiteralPath $scrollCheck } }
}
if ($QueuePreview -or $Test) {
    $queueCheck = Join-Path ([IO.Path]::GetTempPath()) ("greyhaven-queue-{0}.exe" -f [guid]::NewGuid())
    try {
        & $compiler @options "/out:$queueCheck" /main:QueueSurrenderChecks @resources @source (Join-Path $root 'tests/QueueSurrenderChecks.cs')
        if ($LASTEXITCODE) { throw '队列与投降检查编译失败。' }
        & $queueCheck (Join-Path $root 'docs')
        if ($LASTEXITCODE) { throw '队列与投降检查未通过。' }
    } finally { if (Test-Path -LiteralPath $queueCheck) { Remove-Item -LiteralPath $queueCheck } }
}
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
            if ($suite -eq 'WindowChecks') { $checks += Join-Path $root 'tests/TankPaintChecks.cs'; $checks += Join-Path $root 'tests/BattlePresentationChecks.cs'; $checks += Join-Path $root 'tests/BuildingArtChecks.cs'; $checks += Join-Path $root 'tests/BuildingV08Checks.cs'; $checks += Join-Path $root 'tests/AnimationChecks.cs'; $checks += Join-Path $root 'tests/RiverbendChecks.cs'; $checks += Join-Path $root 'tests/RiverbendPlayChecks.cs' }
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
