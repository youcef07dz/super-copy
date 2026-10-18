# Progress

## Shipped
- Explorer integration: "Copy to dest..." verb on `*` + `Directory` → `/todevice "%1"` → isolated device clip (`clipboard.device.txt`) → destination `FolderBrowserDialog` → copy. Registry verb text verified. Internal names (`ToDevice`, `/todevice`) kept by design.
- Settings dialog: compact mode, concurrency 1–32, conflict mode (incl. 4 "ask first"), Robocopy engine, auto-close, Reset-to-default. Defaults single-source `const`s.
- Error report: button shown on `Errors > 0` → modal popup with `CopyJob.ErrorReport()` + copy-to-clipboard.
- Safety fixes A–D: queue delete-after-enqueue; atomic `settings.ini`/cliplist writes; overwrite/move backup+restore; same-drive move `File.Replace`.
- EXE icon: `SuperCopy.ico` (32x32) + `/win32icon` in `build.cmd`; embedded in build + installed copies.
- build.cmd: `/langversion:ISO-2` only to v3.5/v4.0 csc (v2.0.50727 rejects it). Builds green with v2.0.50727 csc.

## Latest (this session)

- Optional installer (`Installer`, `SuperCopy.cs`): `/setup` copies the exe to `%ProgramFiles%\SuperCopy` (elevates via `Admin.Relaunch("/setup")`), registers the Explorer menu pointing at that exe, creates a desktop shortcut (WScript.Shell COM) and an `HKLM\...\Uninstall\SuperCopy` "Apps & features" entry; `/uninstall` removes menu + shortcut + entry + files (in-use exe deleted on reboot via `MoveFileEx`). Settings: "Install app..." button; startup prompt now installs there.
- Menu install no longer copies the exe into `%APPDATA%` (`ShellIntegration.Install(exe)` registers the exe path; icon = `exe,0`); removed `IconMaker.WriteIco`/`Paths.IcoFile`. Portable `/install` (per-user, no admin) is unchanged.
- Verified: build (v2.0.50727) + `/selftest` exit 0.

- About button in Settings (`OnAbout`): modal with name/version, description, exe build time, repo URL. Added `sc3.png` (compact UI) to the README Screenshots table.
- AV false positives (Arctic Wolf/McAfee/SecureAge flagged the exe): removed the download-and-swap update path (the dropper pattern). `Updates.OfferUpdate` now opens the GitHub releases page in the browser; `ExeUrl`, `ApplyAndRestart`, `DelayedCopy` deleted; `CheckLocal` does a direct `File.Copy` only (error asks the user to close the other window). Added publisher metadata (`AssemblyCompany`/`Product`/`Copyright`). Build + `/selftest` exit 0.
- Still to do: Authenticode signing needs a real cert (self-signed does not help AV reputation).

- Drag & drop: drop now shows a 2-item menu at the cursor — "1. Same destination (last job's folder)" (immediate new CopyJob) / "2. Another destination..." (browse dialog seeded with that folder); no job yet → browse directly. `lastDest` set in `AddJob`, so it covers Explorer-verb and `/queue` jobs too; long paths truncated to 40 chars in the label.
- Race fix in `DoCopy` ask path: after winning the `AskPrompting` CAS, re-read `Settings.AskSession` before asking — a worker descheduled between reading `AskSession < 0` and its CAS could win after the first worker finished and prompt a 2nd time (seen once as flaky `/selftest` exit 20 under load; now stable, exit 0 with a copy running). Shortcut: cancel path (`-1`) still leaves `AskSession` unset, so a rare second ask after cancel is possible; upgrade if it ever matters.
- Installed-exe refresh while user's copy runs: rename running `SuperCopy.exe` → `SuperCopy.old.exe` (rename works on locked exe), copy new one in, delete `.old` later; `installed selftest exit: 0`.
- Note: the user's currently open window still runs the old binary until relaunch — drop menu + race fix take effect after restart.

- Update popups (all paths show a modal ask-user-to-download): startup GitHub check → `Updates.OfferUpdate` ("Version vX available… Download and install now?"); manual "Check for updates" button → same `OfferUpdate`; offline fallback `Updates.CheckLocal` compares the running exe vs `%APPDATA%` copy via `FileVer` (assembly major version = `Updates.Current` mirror; `AssemblyVersion` comment enforces sync) and asks to update the installed copy directly (no internet; direct `File.Copy`, `DelayedCopy` fallback when the target is in use).
- Startup order: missing `%APPDATA%` exe → "Install now?"; else `CheckLocal` popup; then background `GetLatest` (5 s, silent offline) → `OfferUpdate` popup when GitHub is newer.
- `/selftest` exit 21: `FileVer` of the running exe ≥ 1, unreadable file → 0. Build + installed exe green (exit 0).
- Limits: releasing v2 requires bumping THREE places (`Updates.Current`, `AssemblyVersion` major, repo `update.txt`); `s/` untracked folder and a deleted `backup.zip` sit in the working tree (not committed).

- >15 multi-select: static cascade (`ExtendedSubCommandsKey`) drops out of the menu on large selections even with `MultiSelectModel=Player` everywhere (docs cap legacy Player at 100, but cascades still vanish on real Explorer — same class of bug that once forced the flat-verb fix in task 30). Reverted to flat top-level verbs: `SuperCopy`/`SuperCut`/`SuperToDevice` on `*`+`Directory`, `SuperPaste` on folder background; `Cascade()` deleted; `IsInstalled` → `*\shell\SuperCopy\command`; selftest guards 12–17 rewritten (flat = ExtendedSubCommandsKey absent, own command, Player). Menu reinstalled, `/selftest` exit 0.
- SettingsForm startup checks (`Shown`, once per window): installed = `File.Exists(Paths.ExeInstalled)` (missing → "Install now?" → `OnInstall` copies exe to `%APPDATA%` + menu); version = `Updates.GetLatest()` on a background thread (`QuickWebClient`, 5 s timeout, silent offline) → newer than `Updates.Current` → `Updates.OfferUpdate` (prompt → download → delayed-copy swap + relaunch). Manual "Check for updates" refactored onto the same `OfferUpdate`.
- Fixed earlier this session: "Ask first" multi-dialog race (`Settings.AskPrompting` CAS in `DoCopy`; `/selftest` exit 20); installed `%APPDATA%` copy was stale during both bugs — always refresh it after a build.
- Verified: build OK (`v2.0.50727`), `/selftest` exit 0 for build **and** installed exe.
- Limits: startup update check hits GitHub on every Settings open (5 s cap); cascade menu is gone (flat verbs only, slightly longer menu); update download overwrites the running exe only if no job is running (existing `shortcut:`).

- Access denied: jobs flag `AccessDenied` (UnauthorizedAccessException in scan/copy/move-delete); on finish `MainForm` asks once "Retry the failed items as administrator?" → re-queues failed items (`QueueStore`) → `Admin.Relaunch()` UAC `runas` into new `/queue` mode (`MainForm(null)`, jobs arrive via `PollQueue`; `retryEscalated` stops the old instance stealing its own queue file). `/selftest` exit 18.
- Update check v1: `Updates.Current=1`; repo root `update.txt` holds the latest version number (`1`); Settings shows "Version 1" + "Check for updates" button → raw GitHub `update.txt` → if newer, prompt → download `SuperCopy.exe` to `%TEMP%` → hidden `ping`-delayed `.cmd` copies over the running exe and relaunches (`Application.Exit` first). TLS 1.2 forced for GitHub. `/selftest` exit 19.
- Verified: build OK (`v2.0.50727`), `/selftest` exit 0.
- Limits: `update.txt` URL hard-coded to `youcef07dz/super-copy` `main`; XP/.NET2 TLS may fail against GitHub (error shown, no crash); updates open the browser (no auto-swap, by design: AV); pushed `SuperCopy.exe` must be rebuilt before push or users get the old binary.

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
