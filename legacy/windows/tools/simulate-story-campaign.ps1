[CmdletBinding()]
param([ValidateSet(2,3)][int]$Mission=3,[ValidateRange(60,3600)][int]$Seconds=900,[string]$Output)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if(-not $Output){$Output=Join-Path $root ('.cache\story-mission-'+$Mission+'-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
[IO.Directory]::CreateDirectory($Output) | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$source=@(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object FullName)
$check=Join-Path ([IO.Path]::GetTempPath()) ('greyhaven-story-simulation-'+[guid]::NewGuid()+'.exe')
try {
 & $compiler /nologo /optimize+ /platform:x64 /codepage:65001 /r:System.Drawing.dll /r:System.Windows.Forms.dll /main:CampaignStorySimulation "/out:$check" @source (Join-Path $root 'tests\CampaignStorySimulation.cs') (Join-Path $root 'tests\AiStandoffSimulation.cs')
 if($LASTEXITCODE){throw '战役模拟编译失败'}
 & $check $Output $Mission $Seconds
 if($LASTEXITCODE){throw '战役模拟未通过'}
 Write-Output ('模拟记录：'+$Output)
}finally{if(Test-Path -LiteralPath $check){Remove-Item -LiteralPath $check}}
