<div align="center">

<p>
  <a href="README.md"><img height="28" src="https://img.shields.io/badge/English-1F6FEB?style=for-the-badge" alt="English"></a>
  <a href="README.ar.md"><img height="28" src="https://img.shields.io/badge/%D8%A7%D9%84%D8%B9%D8%B1%D8%A8%D9%8A%D8%A9-555?style=for-the-badge" alt="العربية"></a>
  <a href="README.fr.md"><img height="28" src="https://img.shields.io/badge/Fran%C3%A7ais-555?style=for-the-badge" alt="Français"></a>
</p>

## ⚡ Generated with the [opencode](https://github.com/anomalyco/opencode) CLI

**Free models: Big Pickle & DeepSeek V4**

# 🚀 Super Copy

**Lightweight, multi-threaded file copier for Windows XP → 11**<br>
with seamless Explorer right-click integration

[![Version 4.0](https://img.shields.io/badge/version-4.0-2EA44F?style=flat-square)](HISTORY.md)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](https://opensource.org/licenses/MIT)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows%20XP%E2%80%9311-0078D4?style=flat-square&logo=windows&logoColor=white)](https://github.com/youcef07dz/super-copy)
[![.NET Framework 2.0](https://img.shields.io/badge/.NET-Framework%202.0-512BD4?style=flat-square&logo=.net&logoColor=white)](https://github.com/youcef07dz/super-copy)
[![Portable](https://img.shields.io/badge/Type-Portable-2EA44F?style=flat-square)](https://github.com/youcef07dz/super-copy)
[![Stars](https://img.shields.io/github/stars/youcef07dz/super-copy?style=flat-square&logo=github&color=yellow)](https://github.com/youcef07dz/super-copy/stargazers)
[![Downloads](https://img.shields.io/github/downloads/youcef07dz/super-copy/total?style=flat-square&logo=github&color=orange)](https://github.com/youcef07dz/super-copy/releases)

[**⬇ Download SuperCopy.exe**](https://github.com/youcef07dz/super-copy/raw/refs/heads/main/SuperCopy.exe) ·
[📖 Features](#-features) ·
[💬 Report Issue](https://github.com/youcef07dz/super-copy/issues) ·
[⭐ Star Project](https://github.com/youcef07dz/super-copy/stargazers)

</div>

> [!TIP]
> **New in v4:** the file list got its **own designed scrollbars** (click to jump, drag the thumb), the file under the mouse gets a proper hover tint, and the whole app is **DPI aware** - crisp list columns and row heights at 125-200% scaling. Verify passes cost about half: the source is hashed *while it is copied*.
>
> **New in v3:** Super Copy can be the **default copy**: press `Ctrl+V` in a folder window and the paste is done by Super Copy (same engine, same conflict rules, XXH3 verify) instead of the shell. Switch it on/off in Settings.
>
> **New in v2:** every copy and move is verified with **XXH3-64**. If a checksum fails, the bad copy is removed, an overwritten file is restored, and your source is kept.

---

<details>
<summary><b>📑 Table of Contents</b></summary>

- [✨ Features](#-features)
- [⚡ Quick Start](#-quick-start)
- [📥 Download](#-download)
- [💻 Command Line](#-command-line)
- [🛠 Build](#-build)
- [📷 Screenshots](#-screenshots)
- [📝 Notes](#-notes)
- [🗂 History](#-history)
- [🤝 Contributing](#-contributing)
- [📄 License](#-license)

</details>

---

## ✨ Features

- ⚡ **Fast** — multi-threaded copying with smart concurrency, auto-tuned by file size.
- 🖱 **Explorer integration** — right-click Copy, Cut/move, Copy to dest… plus **Super Paste here** on a folder background.
- ⭐ **Default copy** — optional background helper: `Ctrl+V` in a folder window is done by Super Copy (on by default, switchable in Settings).
- 🔧 **Two engines** — built-in copier or optional Windows Robocopy.
- ⚖ **Conflict handling** — overwrite, skip, keep both (rename), overwrite if newer, or ask first.
- 📊 **Smart ordering** — big files first for better throughput.
- 🛡 **Verified safety** — XXH3-64 check on every copy and move, free-space guard, and backup/restore on overwrite.
- 🎯 **Convenience** — drag & drop, compact mode, error report with copy-to-clipboard.
- 🌍 **Languages** — English, Arabic, French, or Auto (follows Windows); full RTL for Arabic.
- 📦 **Portable** — one file, no installer, no admin rights; settings and clipboard are written atomically.

---

## ⚡ Quick Start

1. **Download** [`SuperCopy.exe`](https://github.com/youcef07dz/super-copy/raw/refs/heads/main/SuperCopy.exe) — one portable file, no installer.
2. **Run it once** — the Settings window opens.
3. **Install the menu** — click **Install menu** (or run `SuperCopy.exe /install`).
4. **Right-click** any file or folder → **Super Copy** → Copy / Cut / Copy to dest…
5. **Paste** — right-click empty space in a folder → **Super Paste here**.
6. **Optional** — in Settings turn on **"Use Super Copy as the default copy"**: from then on `Ctrl+V` in a folder window is copied by Super Copy.

> [!TIP]
> No admin rights required — the menu is installed per-user in `HKEY_CURRENT_USER`.

---

## 📥 Download

<div align="center">

### 🎯 Direct Download

**SuperCopy.exe** — one portable file

<a href="https://github.com/youcef07dz/super-copy/raw/refs/heads/main/SuperCopy.exe">
  <img src="https://img.shields.io/badge/⬇_Download-SuperCopy.exe-2EA44F?style=for-the-badge&logo=windows&logoColor=white" alt="Download SuperCopy.exe">
</a>

<sub>No installer · No admin rights · Just run</sub>

</div>

<details>
<summary><b>SHA-256 checksum</b></summary>

```text
CBF104E6FD4D09F50D6E70536A5889710495DCA7E4C93D23EC78822732ED3B6D
```

</details>

---

## 💻 Command Line

> [!NOTE]
> `SuperCopy.exe` accepts the following arguments:

- **`SuperCopy.exe`** — open Settings.
- **`SuperCopy.exe /install`** — add Explorer right-click entries.
- **`SuperCopy.exe /uninstall`** — remove Explorer right-click entries.
- **`SuperCopy.exe /copy <paths...>`** — remember items to copy.
- **`SuperCopy.exe /cut <paths...>`** — remember items to move.
- **`SuperCopy.exe /paste <folder>`** — copy/move remembered items here.
- **`SuperCopy.exe /todevice`** — copy selected items to a chosen folder.
- **`SuperCopy.exe /agent`** — run the background helper that handles `Ctrl+V` in Explorer.
- **`SuperCopy.exe /selftest`** — run a self-check (exit 0 if OK).

### Examples

```bash
# Install Explorer context menu entries
SuperCopy.exe /install

# Remember files/folders for copying
SuperCopy.exe /copy "C:\Folder\file1.txt" "D:\Photos\image.jpg"

# Paste remembered items to a destination
SuperCopy.exe /paste "E:\Backup\2026"

# Verify installation health
SuperCopy.exe /selftest
```

---

## 🛠 Build

Run `build.cmd` from the repo root.

> [!NOTE]
> Uses the **.NET Framework 3.5 `csc.exe`** (from the `v3.5` folder) with an explicit `asInvoker` **`app.manifest`** (no elevation prompt). Embeds `SuperCopy.ico` and outputs `SuperCopy.exe` to the repo root.

### Build Requirements

- Windows XP or later
- .NET Framework 3.5 installed (provides `csc.exe` for building)
- Runtime target is .NET Framework 2.0+ (pre-installed on Windows since Vista)
- No external NuGet packages required

---

## 📷 Screenshots

### Main window

![Main window](screenshot/sc2.png)

### Compact mode

![Compact mode](screenshot/sc3.png)

### Settings

![Settings](screenshot/sc1.png)

---

## 📝 Notes

> [!IMPORTANT]
> **Windows 11 users**: context menu entries appear under *Show more options* (or press `Shift + F10`).

> [!NOTE]
> **Settings storage**: stored in `%APPDATA%\SuperCopy\settings.ini`.

> [!NOTE]
> **Installed copy**: the Explorer menu and the helper run from `%APPDATA%\SuperCopy\SuperCopyHelper.exe` - a name of its own, so the task manager tells it apart from the app you start. `/uninstall` stops the helper and removes that file.

> [!NOTE]
> **Default copy helper**: it starts with Windows through the per-user entry `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run` (`SuperCopyHelper.exe /agent`) and stops itself when the option is switched off.

> [!TIP]
> **Menu installation**: requires `/install` (or the **Install menu** button in Settings). **No admin rights required.**

---

## 🗂 History

What's new in v4 and older releases: [HISTORY.md](HISTORY.md). v1, v2 and v3 source and binary are archived in [`old/v1`](old/v1), [`old/v2`](old/v2) and [`old/v3`](old/v3).

---

## 🤝 Contributing

Contributions are welcome! Here's how you can help:

1. 🍴 **Fork** the repository.
2. 🌿 **Create** a feature branch (`git checkout -b feature/amazing-feature`).
3. 💾 **Commit** your changes (`git commit -m 'Add amazing feature'`).
4. 📤 **Push** to the branch (`git push origin feature/amazing-feature`).
5. 🔄 **Open** a Pull Request.

### Ways to Contribute

- 🐛 [Report bugs](https://github.com/youcef07dz/super-copy/issues/new?labels=bug) — help us improve stability.
- 💡 [Suggest features](https://github.com/youcef07dz/super-copy/issues/new?labels=enhancement) — share your ideas.
- ⭐ [Star the project](https://github.com/youcef07dz/super-copy/stargazers) — show your support.
- 📢 Share with others — spread the word.

---

## 📄 License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for details.

<div align="center">

**MIT License — Free for personal and commercial use**

Made with ❤ by [**@youcef07dz**](https://github.com/youcef07dz)

[⬆ Back to top](#-super-copy)

</div>
