<<<<<<< HEAD
# Super Copy

Lightweight multi-threaded file copier for Windows XP .. 11 (.NET Framework 2.0, single-file executable built with `csc.exe`).

## Features

- Smart concurrency: auto-adjusts concurrent file copies by file size (small files copy faster in parallel, large files in single stream)
- Multi-selection support via Explorer right-click menu
- Progress window with per-file bars, overall progress, speed and ETA
- Queue multiple paste operations
- Conflict resolution with "Ask first" that remembers your choice for the session
- Optional Robocopy engine
- Compact/header-only view, auto-close on finish, system tray support
- No admin rights needed (uses `HKCU` for Explorer menu)

## Download

Grab the latest release from GitHub:  
https://github.com/youcef07dz/super-copy/releases

Or clone directly with git:

```bash
git clone https://github.com/youcef07dz/super-copy.git
```

## Build

1. Double-click `build.cmd` (uses `%WINDIR%\Microsoft.NET\Framework\v2.0.50727\csc.exe`)
2. Output: `SuperCopy.exe` (single file, no dependencies)

## Install Explorer Menu

- Run `SuperCopy.exe` → "Install menu"  
- Or: `SuperCopy.exe /install` / `SuperCopy.exe /uninstall`

A stable copy is kept in `%APPDATA%\SuperCopy\`. Windows 11 shows entries under "Show more options" (Shift+F10).

## Usage

1. Right-click files/folders → `Super Copy` → `Super Copy` (or `Super Cut (move)`)
2. Right-click empty space in destination folder → `Super Paste here`
3. Watch progress (overall + per-file). Use `[Hide]` to send to tray.

## Smart Concurrency

When enabled (default), concurrency is chosen per file:

- `< 500 KB` → up to 10 (tapers 10→5)
- `500 KB – 100 MB` → tapers 5→1
- `> 100 MB` → 1

This matches your recommendation: more parallelism for small files, single stream for very large files.

## Settings

Open from the progress window or run `SuperCopy.exe`. Options include:

- Smart concurrency (default) or fixed concurrent copies (1–32)
- Buffer size per thread
- Conflict behavior (overwrite/skip/keep both/overwrite if newer/ask first)
- Copy engine (built-in/Robocopy)
- Auto-close when finished

Stored in `%APPDATA%\SuperCopy\settings.ini`

## Notes

- Windows XP: no empty-background right-click in some cases — right-click the destination folder instead
- Very large multi-selects can hit Windows menu limits on older versions
- Same-drive "Super Cut" is instant rename; cross-drive copies then deletes
- Self-test: `SuperCopy.exe /selftest` returns 0 on success

## License

MIT
=======
# super-copy
>>>>>>> origin/main
