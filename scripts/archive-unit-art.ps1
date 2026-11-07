[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$archiveDir = Join-Path $root 'art/archive/unit-art-history-20261106'
$zipPath = Join-Path $archiveDir '旧版单位素材.zip'
if (Test-Path -LiteralPath $zipPath) { throw '归档已经存在，不覆盖历史记录。' }
New-Item -ItemType Directory -Path $archiveDir -Force | Out-Null
$files = @()
foreach ($folder in @('art/source/v01','art/source/readable-units')) {
    $files += Get-ChildItem -LiteralPath (Join-Path $root $folder) -File -Recurse
}
$keys = @('squad','at','engineer','ifv','tank','arty','mcv','miner','miner-full')
foreach ($folder in @('art/source/v02/sprites','art/source/isometric','art/source/isometric/packed','art/source/aligned/packed')) {
    $files += Get-ChildItem -LiteralPath (Join-Path $root $folder) -File | Where-Object { ($_.BaseName -replace '^(iso-|hd-)','') -in $keys }
}
foreach ($file in @('art/source/v02/models.js','art/source/v02/bake.html','art/source/v02/README.md','art/source/aligned/bake.js','art/source/aligned/tank-drive.js','art/source/aligned/chassis.js','art/source/aligned/ore-models.js','art/source/aligned/README.md','art/source/isometric/README.md','art/source/console/portraits.png')) {
    if (Test-Path -LiteralPath (Join-Path $root $file)) { $files += Get-Item -LiteralPath (Join-Path $root $file) }
}
$files = @($files | Sort-Object FullName -Unique)
$manifest = @($files | ForEach-Object {
    [ordered]@{ path=$_.FullName.Substring($root.Length+1).Replace('\','/'); bytes=$_.Length; sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$json = ConvertTo-Json -Depth 5 -InputObject $manifest
[IO.File]::WriteAllText((Join-Path $archiveDir 'manifest.json'),$json,(New-Object Text.UTF8Encoding($false)))
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::Open($zipPath,[IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($entry in $manifest) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,(Join-Path $root $entry.path),$entry.path,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $zip.Dispose() }
# Read each archived entry back and compare bytes; source files stay in place.
$zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    foreach ($entry in $manifest) {
        $stream = $zip.GetEntry($entry.path).Open()
        $hash = [Security.Cryptography.SHA256]::Create()
        try { $actual = [BitConverter]::ToString($hash.ComputeHash($stream)).Replace('-','') }
        finally { $hash.Dispose(); $stream.Dispose() }
        if ($actual -ne $entry.sha256) { throw "归档校验失败：$($entry.path)" }
    }
} finally { $zip.Dispose() }
Get-FileHash -LiteralPath $zipPath -Algorithm SHA256 | Select-Object Hash,Path
Write-Output ("已归档并逐项核对 {0} 个文件；原文件未移动或删除。" -f $manifest.Count)
