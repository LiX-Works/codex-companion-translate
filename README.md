# Codex Companion Translate

[English](README.md) · [简体中文](README.zh-CN.md)

**A small Windows tray companion for Codex:** copy Chinese text, press **Ctrl+Q**, and paste the English translation. Configure translation rules, model, and reasoning separately from your main conversation.

## Workflow

Copy Chinese → press **Ctrl+Q** → wait for the completion notice → paste English. A separate CLI request uses custom settings and selected examples. The five-second popup shows status, not a translation preview.

One recent Ctrl+Q test completed in 6.9 seconds. This is a single observation, not a speed guarantee.

If you copy something else while translation is running, that newer clipboard content is preserved. You can copy the completed translation from the popup or tray menu.

## Screenshot

![Translation status popup](assets/notification.png)

## Download or build

- **Portable release:** [Get the latest ZIP](https://github.com/LiX-Works/codex-companion-translate/releases/latest), extract it, and run `CodexClipboardTranslator.exe`.
- **Source:** clone the [repository](https://github.com/LiX-Works/codex-companion-translate), sign in to Codex CLI with your ChatGPT account (`codex login` if needed), then run at the repository root:

  ```powershell
  ./build.ps1 -Start
  ```

The source build creates a desktop shortcut named **Codex 伴随翻译** and starts the app. The app uses Windows .NET Framework 4 and WinForms.

## How to use and customize

An existing [Codex CLI](https://learn.chatgpt.com/docs/non-interactive-mode) must be installed and signed in before translating. Launch the portable EXE or the source-build desktop shortcut, copy your text, and press **Ctrl+Q**. Change the hotkey if another app already uses it. Edit `translation-instructions.txt` for terminology and style; adjust other settings in `settings.json` or the tray menu.

| Setting | Default |
|---|---|
| Hotkey | `Ctrl+Q` |
| Model / reasoning | `gpt-6-luna` / `low` |
| Fast mode | Requested; availability and speed depend on account and service |
| Context window | 100,000 tokens |
| Popup | 5 seconds |
| Recent examples | Up to 8, totaling 12,000 characters |
| Proxy | Empty; optional app-specific URL |

The optional proxy is app-specific and leaves Windows settings alone.

## Account, data, and limits

This is an independent project, not an official OpenAI product. It uses the ChatGPT sign-in already configured in Codex CLI, requires no separate developer API key, and does not fall back to one. Service access, model availability, and account limits follow the CLI client, account, and service.

Translation rules and a limited source/translation history stay local. The current text and selected recent examples may be sent through Codex CLI for translation. The app does not process images or rich clipboard content, perform speech recognition, or automatically submit translations to Claude or another assistant.

## License

MIT. Keep the `Copyright (c) 2026 LiX-Works` notice and license text with redistributed copies.



