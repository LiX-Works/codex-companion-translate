param(
    [string]$Destination = "$env:USERPROFILE\.local\share\codex-clipboard-translator",
    [switch]$NoShortcut,
    [switch]$Start
)
$ErrorActionPreference = 'Stop'
$sourceDir = $PSScriptRoot
$targetDir = [IO.Path]::GetFullPath($Destination)
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework C# 编译器不可用；未安装其他环境。' }
$exe = Join-Path $targetDir 'CodexClipboardTranslator.exe'
$targetRunning = @(Get-CimInstance Win32_Process -Filter "Name='CodexClipboardTranslator.exe'" | Where-Object { $_.ExecutablePath -and [IO.Path]::GetFullPath($_.ExecutablePath) -eq $exe })
if ($targetRunning.Count -gt 0) {
    throw '翻译工具正在运行。请先在托盘菜单选择“退出”，再重新构建。'
}
New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
$brandIcon = Join-Path $sourceDir 'assets\translator.ico'
if (-not (Test-Path -LiteralPath $brandIcon)) { throw '缺少品牌图标；请先生成 assets。' }
$manifest = Join-Path $sourceDir 'app.manifest'
& $compiler /nologo /langversion:5 /target:winexe /optimize+ /platform:anycpu "/win32icon:$brandIcon" "/win32manifest:$manifest" "/out:$exe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll (Join-Path $sourceDir 'App.cs') (Join-Path $sourceDir 'Backend.cs') (Join-Path $sourceDir 'Popup.cs') (Join-Path $sourceDir 'Branding.cs') (Join-Path $sourceDir 'MotionGlyph.cs') (Join-Path $sourceDir 'TranslationReader.cs')
if ($LASTEXITCODE -ne 0) { throw '编译失败。' }
foreach ($name in @('settings.json', 'translation-instructions.txt', 'translation-instructions.zh-CN.txt')) {
    $path = Join-Path $targetDir $name
    if (-not (Test-Path -LiteralPath $path)) { Copy-Item -LiteralPath (Join-Path $sourceDir $name) -Destination $path }
}
Copy-Item -LiteralPath (Join-Path $sourceDir 'README.md') -Destination (Join-Path $targetDir 'README.md') -Force
Copy-Item -LiteralPath (Join-Path $sourceDir 'launch.ps1') -Destination (Join-Path $targetDir 'launch.ps1') -Force
New-Item -ItemType Directory -Path (Join-Path $targetDir 'work') -Force | Out-Null
$validationDir = Join-Path $targetDir ('validation\build-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0,6))
$test = Start-Process -FilePath $exe -ArgumentList @('--self-test', '--output', ('"' + $validationDir + '"')) -WindowStyle Hidden -PassThru -Wait
if ($test.ExitCode -ne 0) { throw "本地自检失败；参阅 $targetDir\test-error.txt" }
Copy-Item -LiteralPath $brandIcon -Destination (Join-Path $targetDir 'translator.ico') -Force
Copy-Item -LiteralPath $brandIcon -Destination (Join-Path $targetDir 'translator-v0.4.ico') -Force
Copy-Item -LiteralPath (Join-Path $sourceDir 'assets\logo.png') -Destination (Join-Path $targetDir 'logo.png') -Force
if (-not $NoShortcut) {
    $desktop = [Environment]::GetFolderPath('Desktop')
    $link = Join-Path $desktop 'Codex 伴随翻译.lnk'
    $shell = New-Object -ComObject WScript.Shell
    $legacyLink = Join-Path $desktop 'Codex 翻译.lnk'
    if ((Test-Path -LiteralPath $legacyLink) -and -not (Test-Path -LiteralPath $link)) {
        $legacyShortcut = $shell.CreateShortcut($legacyLink)
        if ($legacyShortcut.TargetPath -eq $exe) { Move-Item -LiteralPath $legacyLink -Destination $link }
    }
    $shortcut = $shell.CreateShortcut($link)
    if (Test-Path -LiteralPath $link) {
        if (-not $shortcut.TargetPath -or $shortcut.TargetPath -ne $exe) { throw "已存在其他或无法确认目标的同名快捷方式，未覆盖：$link" }
    }
    $shortcut.TargetPath = $exe
    $shortcut.WorkingDirectory = $targetDir
    $shortcut.IconLocation = (Join-Path $targetDir 'translator-v0.4.ico') + ',0'
    $shortcut.Description = 'Ctrl+Q 中译英，Ctrl+E 英译中；托盘菜单可退出。'
    $shortcut.Save()
}
if ($Start) { & (Join-Path $sourceDir 'launch.ps1') -ProgramPath $exe }
[pscustomobject]@{Program=$exe; SelfTest='passed'; DesktopShortcut=(-not $NoShortcut); Started=[bool]$Start} | ConvertTo-Json -Compress
