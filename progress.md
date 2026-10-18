# Progress

## Shipped
- Explorer integration: "Copy to dest..." verb on `*` + `Directory` → `/todevice "%1"` → isolated device clip (`clipboard.device.txt`) → destination `FolderBrowserDialog` → copy. Registry verb text verified. Internal names (`ToDevice`, `/todevice`) kept by design.
- Settings dialog: compact mode, concurrency 1–32, conflict mode (incl. 4 "ask first"), Robocopy engine, auto-close, Reset-to-default. Defaults single-source `const`s.
- Error report: button shown on `Errors > 0` → modal popup with `CopyJob.ErrorReport()` + copy-to-clipboard.
- Safety fixes A–D: queue delete-after-enqueue; atomic `settings.ini`/cliplist writes; overwrite/move backup+restore; same-drive move `File.Replace`.
- EXE icon: `SuperCopy.ico` (32x32) + `/win32icon` in `build.cmd`; embedded in build + installed copies.
- build.cmd: `/langversion:ISO-2` only to v3.5/v4.0 csc (v2.0.50727 rejects it). Builds green with v2.0.50727 csc.

## Latest (this session)

- Branch: `main` tracks `origin/master`, HEAD `0f2b5b0`, working tree clean; no push unless user requests.
- Explorer menu: copy actions under one cascaded `Super Copy >` on files and folders (`*\shell\SuperCopy`, `Directory\shell\SuperCopy`) — Copy, Cut (move), Copy to dest…, built by `ShellIntegration.Cascade` (`ExtendedSubCommandsKey` → the parent's own `shell` subkey, `MUIVerb="Super Copy"`, empty parent default).
- Super Paste is **outside** the submenu and empty-space only: flat verb `Directory\Background\shell\SuperPaste` → `/paste "%V"` (`Verb` helper). Removed from the folder submenu and from the drive root (`Drive\shell\SuperCopy` gone entirely); `Directory\Background\shell\SuperCopy` retired.
- Multi-select bug fixed at the root: the old cascade parents had no `MultiSelectModel`, so the default Document model dropped the submenu on a multi-selection and Explorer ran the selection's default verb. `MultiSelectModel="Player"` is now on every parent **and** leaf (cap 100).
- `Install` wipes the `AllKeys`+`OldKeys` trees before rewriting; `IsInstalled` checks `*\shell\SuperCopy\shell\Copy\command`.
- Verified: build OK (`v2.0.50727`); `/selftest` exit 0 for build **and** installed exe (exit 12–17 guards: `ExtendedSubCommandsKey`, `Player`, leaf command, paste flat without submenu, retired keys absent); `/install`; `reg query` (both views) shows the tree; installed exe refreshed to SHA-256 `9B52B540…`.
- Earlier this session: drag & drop (queues a new `CopyJob`; `FolderBrowserDialog` pre-seeded with the running job's dest); big-first scan order; free-space check; move-verify hash; `/selftest` exits 6–11.
- Limits: drop always copies (`Move=false`), no dedup against existing sources, a dropped path may equal/contain the dest (`shortcut:` in code). Legacy Player caps at 100 selected items.

## Pending
- User visual check: cascade on a single file / folder / 16+ multi-selection; "Super Paste here" on empty folder background; no entry on a drive root; plus Settings window, Robocopy, Error report popup, "Copy to dest..." picker, big-first order, drag & drop.

## Known gaps (accepted)
- `/todevice` silently no-ops if a SuperCopy GUI is already open (single-instance mutex path).
- Two items can resolve to the same destination path (incl. `UniqueFile` race).
- Free-space estimate is approximate (conflict=2 keep-both can under-count).
