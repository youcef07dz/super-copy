# Super Copy

Lightweight, multi-threaded file copier for Windows XP..11 with Explorer right-click integration.

[**→ Download SuperCopy.exe**](SuperCopy.exe)

## Quick Start

- [Download SuperCopy.exe](SuperCopy.exe) — single portable .exe, no installer required.
- Run SuperCopy.exe to open Settings.
- Right-click files/folders → *Super Copy > Copy/Cut (move)/Copy to dest...*
- Right-click empty space in a folder → *Super Paste here*

## Features

- **Fast**: multi-threaded copying with smart concurrency (auto-tuned by file size)
- **Explorer integration**: cascade menu (Copy, Cut (move), Copy to dest...) + *Super Paste here* on folder background
- **Engines**: built-in or optional Windows Robocopy
- **Conflicts**: Overwrite / Skip / Keep both (rename) / Overwrite if newer / Ask first
- **Smart ordering**: big files first for better throughput
- **Safety**: free-space check, move-verify (MD5) before deleting source
- **Convenience**: drag & drop, compact mode, error report with copy-to-clipboard
- **Screenshots**: in-app capture (copy to clipboard + optional auto-save)
- **Portable + safe**: atomic I/O for settings/clipboard; uses HKCU for menu

## Download

- **[Direct Download → SuperCopy.exe](SuperCopy.exe)**

## Command Line

`	ext
SuperCopy.exe                    Open Settings
SuperCopy.exe /install           Add Explorer right-click entries
SuperCopy.exe /uninstall         Remove Explorer right-click entries
SuperCopy.exe /copy <paths...>   Remember items to copy
SuperCopy.exe /cut  <paths...>   Remember items to move
SuperCopy.exe /paste <folder>    Copy/move remembered items here
SuperCopy.exe /todevice          Copy selected items to chosen folder
SuperCopy.exe /selftest          Run self-check (exit 0 if OK)
`

## Build

`at
build.cmd
`

Uses .NET Framework 2.0 csc.exe (built into Windows). Embeds SuperCopy.ico and outputs SuperCopy.exe to the repo root.

## Notes

- **Windows 11**: context menu entries appear under *Show more options* (Shift+F10).
- **Settings**: stored in %APPDATA%\SuperCopy\settings.ini.
- **Menu**: requires /install (or *Install menu* in Settings). No admin rights required.

## License

MIT