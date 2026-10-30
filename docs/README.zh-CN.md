# Codex 伴随翻译

[English](README.md) · [简体中文](README.zh-CN.md)

**为 Codex 工作流准备的轻量 Windows 托盘工具：**按 **Ctrl+Q** 可将复制的中文译成英文，按 **Ctrl+E** 可将复制的英文译成简体中文。翻译规则、模型和推理强度独立于主对话设置。

## 工作流程

**中文 → 英文：**复制中文 → 按 **Ctrl+Q** → 等待完成提示 → 粘贴英文。工具通过独立的 Codex CLI 请求翻译，并使用专属设置和选中的近期示例。264 × 64 的紧凑提示窗只显示状态、不显示译文，五秒后自动关闭。

**英文 → 简体中文：**复制英文并按 **Ctrl+E**。屏幕中央的原生阅读窗会以 14 pt 字号显示完整译文，按实际换行自适应高度；长译文可滚动查看，不会截断。窗口不会自动隐藏，按 **Esc** 或 **X** 关闭。完整译文会自动复制到剪贴板。

翻译期间如果又复制了其他内容，工具会保留新的剪贴板；需要时可从阅读窗、提示窗或托盘菜单手动复制完整译文。

## 截图

![Ctrl+Q 翻译状态提示窗](assets/notification.png)

![Ctrl+E 简体中文阅读窗](assets/chinese-reader.png)

## 下载或构建

- **便携版：**[下载最新 ZIP](https://github.com/LiX-Works/codex-companion-translate/releases/latest)，解压后运行 `CodexClipboardTranslator.exe`。
- **源码：**克隆[项目仓库](https://github.com/LiX-Works/codex-companion-translate)，用 ChatGPT 账号登录 Codex CLI（如未登录，可运行 `codex login`），再在仓库根目录执行：

  ```powershell
  ./build.ps1 -Start
  ```

源码构建会启动程序，并创建名为 **Codex 伴随翻译** 的桌面快捷方式。程序使用 Windows .NET Framework 4 和 WinForms。

从旧便携版升级时，请保留自定义 `settings.json`、翻译规则和历史，新增 `translation-instructions.zh-CN.txt` 文件。源码构建升级时，缺少的 `ChineseHotkey` 默认设为 `Ctrl+E`，仅在缺少时复制规则文件。

## 使用与自定义

翻译前需安装并登录已有的 [Codex CLI](https://learn.chatgpt.com/docs/non-interactive-mode)。运行便携版 EXE 或源码构建生成的桌面快捷方式，复制文本后按 **Ctrl+Q** 或 **Ctrl+E**。两个快捷键均可配置；若其他应用已将组合注册为系统热键，Windows 可能无法注册。编辑 `translation-instructions.txt` 设置中译英规则，编辑 `translation-instructions.zh-CN.txt` 设置英译中规则。历史示例按当前方向分别参考；旧记录默认作为中译英读取。其他选项可在 `settings.json` 或托盘菜单中调整。

| 设置 | 默认值 |
|---|---|
| 中译英快捷键 | `Ctrl+Q` |
| 英译中快捷键（`ChineseHotkey`） | `Ctrl+E` |
| 模型 / 推理强度 | `gpt-6-luna` / `low` |
| Fast 模式 | 请求启用；可用性和速度取决于账号与服务 |
| 上下文窗口 | 100,000 tokens |
| 状态提示窗 / 中文阅读窗 | 5 秒 / 按 `Esc` 或 `X` 关闭 |
| 近期示例 | 最多 8 条，合计不超过 12,000 字符 |
| 代理 | 默认留空；可选应用专用地址 |

可选代理只供本工具使用，不修改 Windows 系统代理。响应时间没有固定保证。

工具运行时，全局快捷键会接管使用同一组合的软件内部功能，包括常见的 Ctrl+E 搜索操作。

## 账号、数据与限制

这是独立项目，并非 OpenAI 官方产品。程序使用 Codex CLI 中现有的 ChatGPT 登录，不需要单独的开发者 API Key，也不会回退到 API Key。服务访问、模型可用性和账号限制取决于 CLI 客户端、登录账号与服务端。

翻译规则和有限的翻译历史保存在本机；示例按当前翻译方向筛选。本次待译文本及选中的近期示例会通过 Codex CLI 发送给翻译服务。程序不处理图片或富文本剪贴板、不做语音识别，也不会自动把译文提交给 Claude 或其他助手。

## 许可证

MIT。再发布时请保留 `Copyright (c) 2026 LiX-Works` 声明和许可证文本。
