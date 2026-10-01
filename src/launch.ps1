param(
    [string]$ProgramPath = "$env:USERPROFILE\.local\share\codex-clipboard-translator\CodexClipboardTranslator.exe"
)
$ErrorActionPreference = 'Stop'
$programFile = [IO.Path]::GetFullPath($ProgramPath)
if (-not (Test-Path -LiteralPath $programFile -PathType Leaf) -or [IO.Path]::GetExtension($programFile) -ne '.exe') {
    throw '未找到翻译工具 exe；未启动任何程序。'
}
$workingFolder = Split-Path -Parent $programFile
# Ask the existing desktop shell to open the app. A direct Start-Process from
# a Codex-owned job can otherwise make the app part of the same cleanup job.
# Desktop .lnk already targets the exe directly and needs no wrapper.
$shell = New-Object -ComObject Shell.Application
$windows = $shell.Windows()
$desktopLocation = 0
$desktopRoot = 0
$desktopWindowHandle = 0
# Use the actual desktop Explorer automation object. Calling ShellExecute on
# a freshly created Shell.Application alone can still launch from this process.
$desktop = $windows.FindWindowSW([ref]$desktopLocation, [ref]$desktopRoot, 8, [ref]$desktopWindowHandle, 1)
if (-not $desktop -or -not $desktop.Document -or -not $desktop.Document.Application) {
    throw '无法连接桌面 Explorer；为避免随 Codex 退出，未使用直接子进程后备。请双击桌面快捷方式。'
}
$desktop.Document.Application.ShellExecute($programFile, '', $workingFolder, 'open', 0)
[pscustomobject]@{Program=$programFile;LaunchRequested=$true;Launcher='Desktop Explorer';StartedVerified=$false} | ConvertTo-Json -Compress
