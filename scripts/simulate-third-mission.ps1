[CmdletBinding()]
param([ValidateRange(60,7200)][int]$Seconds=1800,[ValidateSet('passive','idle')][string]$Mode='passive',[ValidateRange(.0333333,.1)][single]$Step=.1,[string]$Output)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if(-not $Output){$Output=Join-Path $root ('.cache\third-mission-'+[DateTime]::Now.ToString('yyyyMMdd-HHmmss'))}
[IO.Directory]::CreateDirectory($Output) | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$source=@(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object FullName)
$check=Join-Path ([IO.Path]::GetTempPath()) ('greyhaven-third-simulation-'+[guid]::NewGuid()+'.exe')
try {
 & $compiler /nologo /optimize+ /platform:x64 /codepage:65001 /r:System.Windows.Forms.dll /r:System.Drawing.dll /main:AiStandoffSimulation "/out:$check" @source (Join-Path $root 'tests\AiStandoffSimulation.cs')
 if($LASTEXITCODE){throw '第三关模拟编译失败'}
 & $check $Output $Seconds $Mode ($Step.ToString([Globalization.CultureInfo]::InvariantCulture))
 if($LASTEXITCODE){throw '第三关模拟失败'}
 Write-Output ('模拟数据保存在 '+$Output)
}finally{if(Test-Path -LiteralPath $check){Remove-Item -LiteralPath $check}}
