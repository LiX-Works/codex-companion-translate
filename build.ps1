param(
    [string]$Destination = "$env:USERPROFILE\.local\share\codex-clipboard-translator",
    [switch]$NoShortcut,
    [switch]$Start
)
$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'src'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.zh-CN.md') -Destination (Join-Path $source 'README.md') -Force
& (Join-Path $source 'build.ps1') -Destination $Destination -NoShortcut:$NoShortcut -Start:$Start
