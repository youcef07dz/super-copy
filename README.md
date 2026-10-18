<div align="center">

# 🚀 Super Copy

**Lightweight, multi-threaded file copier for Windows XP → 11**<br>
with seamless Explorer right-click integration

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](https://opensource.org/licenses/MIT)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows%20XP%E2%80%9311-0078D4?style=flat-square&logo=windows&logoColor=white)](https://github.com/youcef07dz/super-copy)
[![.NET Framework 2.0](https://img.shields.io/badge/.NET-Framework%202.0-512BD4?style=flat-square&logo=.net&logoColor=white)](https://github.com/youcef07dz/super-copy)
[![Portable](https://img.shields.io/badge/Type-Portable-2EA44F?style=flat-square)](https://github.com/youcef07dz/super-copy)
[![No Installer](https://img.shields.io/badge/Installer-Not%20Required-2EA44F?style=flat-square)](https://github.com/youcef07dz/super-copy)
[![Admin Rights](https://img.shields.io/badge/Admin%20Rights-Not%20Required-2EA44F?style=flat-square)](https://github.com/youcef07dz/super-copy)
[![Stars](https://img.shields.io/github/stars/youcef07dz/super-copy?style=flat-square&logo=github&color=yellow)](https://github.com/youcef07dz/super-copy/stargazers)
[![Downloads](https://img.shields.io/github/downloads/youcef07dz/super-copy/total?style=flat-square&logo=github&color=orange)](https://github.com/youcef07dz/super-copy/releases)

[**⬇ Download SuperCopy.exe**](https://github.com/youcef07dz/super-copy/raw/refs/heads/main/SuperCopy.exe) ·
[📖 Documentation](#-documentation) ·
[💬 Report Issue](https://github.com/youcef07dz/super-copy/issues) ·
[⭐ Star Project](https://github.com/youcef07dz/super-copy/stargazers)

</div>

---

## 📑 Table of Contents

- [✨ Features](#-features)
- [⚡ Quick Start](#-quick-start)
- [📥 Download](#-download)
- [💻 Command Line](#-command-line)
- [🛠 Build](#-build)
- [📷 Screenshots](#-screenshots)
- [📝 Notes](#-notes)
- [🤝 Contributing](#-contributing)
- [📄 License](#-license)

---

## ✨ Features

|  | Feature | Description |
|:---:|---|---|
| ⚡ | **Fast** | Multi-threaded copying with smart concurrency, auto-tuned by file size |
| 🖱 | **Explorer integration** | Right-click verbs (Copy, Cut/move, Copy to dest…) + *Super Paste here* on folder background |
| 🔧 | **Engines** | Built-in engine or optional Windows Robocopy |
| ⚖ | **Conflicts** | Overwrite · Skip · Keep both (rename) · Overwrite if newer · Ask first |
| 📊 | **Smart ordering** | Big files first for better throughput |
| 🛡 | **Safety** | Free-space check, move-verify (MD5) before deleting source |
| 🎯 | **Convenience** | Drag & drop, compact mode, error report with copy-to-clipboard |
| 📦 | **Portable + safe** | Atomic I/O for settings/clipboard; uses HKCU for menu |

---

## ⚡ Quick Start

```
1. Download        →  SuperCopy.exe (single portable file, no installer)
2. Run once        →  Opens Settings panel
3. Install menu    →  Click "Install menu" in Settings (or run with /install)
4. Right-click     →  Files/folders → Super Copy > Copy / Cut / Copy to dest...
5. Super Paste     →  Right-click empty space in a folder → Super Paste here
```

> [!TIP]
> No admin rights required — the menu is installed per-user in `HKEY_CURRENT_USER`.

---

## 📥 Download

<table>
  <tr>
    <td align="center" width="100%">
      <h3>🎯 Direct Download</h3>
      <p><b>SuperCopy.exe</b> — single portable file</p>
      <a href="https://github.com/youcef07dz/super-copy/raw/refs/heads/main/SuperCopy.exe">
        <img src="https://img.shields.io/badge/⬇_Download-SuperCopy.exe-2EA44F?style=for-the-badge&logo=windows&logoColor=white" alt="Download SuperCopy.exe">
      </a>
      <p><sub>No installer · No admin rights · Just run</sub></p>
    </td>
  </tr>
</table>

---

## 💻 Command Line

> [!NOTE]
> The `SuperCopy.exe` accepts the following command-line arguments:

| Command | Action |
|---|---|
| `SuperCopy.exe` | Open Settings |
| `SuperCopy.exe /install` | Add Explorer right-click entries |
| `SuperCopy.exe /uninstall` | Remove Explorer right-click entries |
| `SuperCopy.exe /copy <paths...>` | Remember items to copy |
| `SuperCopy.exe /cut <paths...>` | Remember items to move |
| `SuperCopy.exe /paste <folder>` | Copy/move remembered items here |
| `SuperCopy.exe /todevice` | Copy selected items to chosen folder |
| `SuperCopy.exe /selftest` | Run self-check (exit 0 if OK) |

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

```bash
build.cmd
```

> [!NOTE]
> Uses **.NET Framework 2.0 csc.exe** (built into Windows). Embeds `SuperCopy.ico` and outputs `SuperCopy.exe` to the repo root.

### Build Requirements

- Windows XP or later
- .NET Framework 2.0+ (pre-installed on Windows since Vista)
- No external NuGet packages required

---

## 📷 Screenshots

<div align="center">

| Main window | Settings |
|:---:|:---:|
| ![Main window](sc2.png) | ![Settings](sc1.png) |

</div>

---

## 📝 Notes

> [!IMPORTANT]
> **Windows 11 users**: Context menu entries appear under *Show more options* (or press `Shift + F10`).

> [!NOTE]
> **Settings storage**: Stored in `%APPDATA%\SuperCopy\settings.ini`.

> [!TIP]
> **Menu installation**: Requires `/install` (or *Install menu* button in Settings). **No admin rights required.**

---

## 🤝 Contributing

Contributions are welcome! Here's how you can help:

1. 🍴 **Fork** the repository
2. 🌿 **Create** a feature branch (`git checkout -b feature/amazing-feature`)
3. 💾 **Commit** your changes (`git commit -m 'Add amazing feature'`)
4. 📤 **Push** to the branch (`git push origin feature/amazing-feature`)
5. 🔄 **Open** a Pull Request

### Ways to Contribute

- 🐛 [Report bugs](https://github.com/youcef07dz/super-copy/issues/new?labels=bug) — help us improve stability
- 💡 [Suggest features](https://github.com/youcef07dz/super-copy/issues/new?labels=enhancement) — share your ideas
- ⭐ [Star the project](https://github.com/youcef07dz/super-copy/stargazers) — show your support
- 📢 Share with others — spread the word

---

## 📄 License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for details.

<div align="center">

```
MIT License — Free for personal and commercial use
```

Made with ❤ by [**@youcef07dz**](https://github.com/youcef07dz)

[⬆ Back to top](#-super-copy)

</div>
