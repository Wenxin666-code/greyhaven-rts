[CmdletBinding()]
param([ValidateRange(60,3600)][int]$Seconds=600,[ValidateRange(.0333333,.1)][float]$Step=(1.0/30),[string]$Output)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if(-not $Output){$Output=Join-Path $root ('.cache/performance-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
[IO.Directory]::CreateDirectory($Output) | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$source=@(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object FullName)
$exe=Join-Path $Output 'measure.exe'
& $compiler /nologo /optimize+ /platform:x64 /codepage:65001 /r:System.Drawing.dll /r:System.Windows.Forms.dll /main:PerformanceBenchmark "/out:$exe" @source (Join-Path $root 'tests/PerformanceBenchmark.cs') (Join-Path $root 'tests/AiStandoffSimulation.cs')
if($LASTEXITCODE){throw '性能测量工具编译失败'}
& $exe $Output $Seconds ($Step.ToString('R',[Globalization.CultureInfo]::InvariantCulture))
if($LASTEXITCODE){throw '性能测量未完成'}
Write-Output "结果保存在：$Output"
