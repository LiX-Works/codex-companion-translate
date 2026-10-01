# Codex 伴随翻译

[English](README.md) · [简体中文](README.zh-CN.md)

**为 Codex 工作流准备的轻量 Windows 托盘工具：**复制中文，按 **Ctrl+Q**，再粘贴英文译文。翻译规则、模型和推理强度可独立设置，不依赖主对话的上下文。

## 工作流程

复制中文 → 按 **Ctrl+Q** → 等待完成提示 → 粘贴英文。工具通过独立的 Codex CLI 请求翻译，并使用专属设置和选中的示例。紧凑提示窗只显示状态、不预览译文，约五秒后自动隐藏。

最近一次 Ctrl+Q 实测耗时 6.9 秒。这只是单次观察，不是速度保证。

翻译期间如果又复制了其他内容，工具会保留新的剪贴板；完成的译文可从提示窗或托盘菜单手动复制。

## 截图

![翻译状态提示窗](assets/notification.png)

## 下载或构建

- **便携版：**[下载最新 ZIP](https://github.com/LiX-Works/codex-companion-translate/releases/latest)，解压后运行 `CodexClipboardTranslator.exe`。
- **源码：**克隆[项目仓库](https://github.com/LiX-Works/codex-companion-translate)，用 ChatGPT 账号登录 Codex CLI（如未登录，可运行 `codex login`），再在仓库根目录执行：

  ```powershell
  ./build.ps1 -Start
  ```

源码构建会启动程序，并创建名为 **Codex 伴随翻译** 的桌面快捷方式。程序使用 Windows .NET Framework 4 和 WinForms。

## 使用与自定义

翻译前需安装并登录已有的 [Codex CLI](https://learn.chatgpt.com/docs/non-interactive-mode)。运行便携版 EXE 或源码构建生成的桌面快捷方式，复制文本并按 **Ctrl+Q**。若其他软件占用了此快捷键，可修改它。编辑 `translation-instructions.txt` 可设置术语和风格；其他选项可在 `settings.json` 或托盘菜单中调整。

| 设置 | 默认值 |
|---|---|
| 快捷键 | `Ctrl+Q` |
| 模型 / 推理强度 | `gpt-6-luna` / `low` |
| Fast 模式 | 请求启用；可用性和速度取决于账号与服务 |
| 上下文窗口 | 100,000 tokens |
| 提示窗 | 5 秒 |
| 近期示例 | 最多 8 条，合计不超过 12,000 字符 |
| 代理 | 默认留空；可选应用专用地址 |

可选代理只供本工具使用，不修改 Windows 系统代理。Fast 模式和上下文设置都不保证固定响应时间。

## 账号、数据与限制

这是独立项目，并非 OpenAI 官方产品。程序使用 Codex CLI 中现有的 ChatGPT 登录，不需要单独的开发者 API Key，也不会回退到 API Key。服务访问、模型可用性和账号限制取决于 CLI 客户端、登录账号与服务端。

翻译规则和有限的原文／译文历史保存在本机。本次待译文本及选中的近期示例可能会通过 Codex CLI 发送给翻译服务。程序不处理图片或富文本剪贴板、不做语音识别，也不会自动把译文提交给 Claude 或其他助手。

## 许可证

MIT。再发布时请保留 `Copyright (c) 2026 LiX-Works` 声明和许可证文本。
