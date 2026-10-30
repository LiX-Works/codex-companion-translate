# Codex Companion Translate 0.5.0

Two clipboard translation workflows, using your existing Codex CLI sign-in:

- **Ctrl+Q:** Chinese to English, with the original compact completion notice.
- **Ctrl+E:** English to Simplified Chinese, with a centered reading window and automatic clipboard replacement.

The Chinese reader uses larger text and adapts its height to the content. Long translations scroll without truncation. Press **Esc** or **Close** to hide it; it does not close automatically. **Copy Full Text** copies the complete displayed result without resetting the reading position.

Each direction has its own instructions and recent examples. Existing settings and history remain compatible.

Download the Windows ZIP and run `CodexClipboardTranslator.exe` from a writable folder. An installed Codex CLI with ChatGPT sign-in is required. When upgrading a portable copy, keep your existing `settings.json`, translation rules and history; add the new `translation-instructions.zh-CN.txt` file.

中文：新增 **Ctrl+E 英译中**，完整中文阅读窗支持滚动、复制全文和 **Esc 手动关闭**。原 **Ctrl+Q 中译英** 保留。两方向的翻译规则与历史示例分别处理。
