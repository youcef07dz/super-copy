# Progress

## Shipped
- Explorer integration: "Copy to dest..." verb on `*` + `Directory` → `/todevice "%1"` → isolated device clip (`clipboard.device.txt`) → destination `FolderBrowserDialog` → copy. Registry verb text verified. Internal names (`ToDevice`, `/todevice`) kept by design.
- Settings dialog: compact mode, concurrency 1–32, conflict mode (incl. 4 "ask first"), Robocopy engine, auto-close, Reset-to-default. Defaults single-source `const`s.
- Error report: button shown on `Errors > 0` → modal popup with `CopyJob.ErrorReport()` + copy-to-clipboard.
- Safety fixes A–D: queue delete-after-enqueue; atomic `settings.ini`/cliplist writes; overwrite/move backup+restore; same-drive move `File.Replace`.
- EXE icon: `SuperCopy.ico` (32x32) + `/win32icon` in `build.cmd`; embedded in build + installed copies.
- build.cmd: `/langversion:ISO-2` only to v3.5/v4.0 csc (v2.0.50727 rejects it). Builds green with v2.0.50727 csc.

## Latest (this session)

- **Queue view**: adding files to the queue now shows them at once — `CopyJob.Prepare()` starts the folder scan as soon as the job is queued (the copy itself still waits for `Start()`), so the file list and the total size are known immediately. The main list is now the list of *all* jobs: `ListCount()`/`ListItem(row, out job)` map a row to the item of the right job, so the files of the running job and of the waiting ones sit one after the other. The header covers the whole queue: total size, file count, progress bar, ETA (remaining bytes / current speed), average speed (`done / total active time`, so it is not divided by the time of a single job anymore) and the elapsed clock; the window title shows the queue percentage. The error report and the "last error" line also cover every job. The list rows also carry the real name and size, so the list is readable for screen readers and for copying a row.
- Removed the second (per-file) progress bar again: the header is back to one bar, height 104.

- Released **v3**: `Updates.Current=3`, `AssemblyVersion/FileVersion=3.0.0.0`, `update.txt=3`; exe rebuilt, installed copy refreshed, SHA-256 `9C02D0A6…`.
- **Default copy**: new `HookAgent` class (`SuperCopy.exe /agent`) — a `WH_KEYBOARD_LL` hook swallows `Ctrl+V` while an Explorer window has the focus (not inside the address bar / rename box), reads the file list and the move flag from the clipboard (`CF_HDROP` + `CF_PREFERREDDROPEFFECT`), resolves the destination through `Shell.Application` (selected folder, else `LocationURL`), and runs the normal `CopyJob` (same engine, conflict rules, XXH3 verify). The shell's own paste is suppressed → no double copy. Settings checkbox "Use Super Copy as the default copy", `DefaultCopy=1` in `settings.ini`, on by default.
- The helper **starts with Windows** while the option is on: `HookAgent.Autostart` writes the per-user `HKCU\...\Run` value `SuperCopy` = `"<exe>" /agent` (removed again when the option is off); the running helper polls `settings.ini` every 2 s and exits when the option is off. `HookAgent.Ensure()` is called on every GUI start so the entry always matches the setting.
- The copy window now **comes on top**: `MainForm.ShowOnTop()` (TopMost + Activate + BringToFront) when a job is added, `TopMost` dropped in `AllDone()` — applies to the default-copy flow, the Explorer verbs and the queue.
- Header shows the progress of the whole queue (one bar): the queue total size, file count, ETA, average speed and the window title are summed over every job; the per-file bar was dropped again (header height back to 104).
- Verified: build (v3.5 csc) + `/selftest` exit 0 (build and installed copy); E2E in a real Explorer window — Ctrl+V copies with Super Copy and the shell does not paste, cut+paste moves the file (source deleted), helper exits when the option is switched off, autostart entry created/removed with the option.
- Fixed along the way: `GetModuleHandle` must come from kernel32 (user32 → "entry point not found"), the hidden helper window needs `ctx.MainForm` (otherwise no message loop → the hook callback never ran) and must be `WS_EX_NOACTIVATE` (it stole the focus from Explorer), COM `HWND` is `Int64` (`Convert.ToInt64`, not `(int)`), and `SelectedItems`/`Document` need `GetProperty | InvokeMethod`.
- Limits: the hook is a low-level keyboard hook → it can trigger AV heuristics (the project is AV-sensitive); the helper is per-user and can be killed by a locked-down endpoint policy; Explorer paste by mouse (right-click → Paste) is not intercepted, only `Ctrl+V`.

- Released **v2**: `Updates.Current=2`, `AssemblyVersion/FileVersion=2.0.0.0`, `update.txt=2`; exe rebuilt and installed copy refreshed (SHA-256 `CED3BDCF…`). Added `HISTORY.md` (v2 what's new + v1 summary) and a History section in all three READMEs. Archived v1 source+binary in `old/v1/`.
- XXH3-64 verification (scalar port of the reference xxhash.h) for every copy and move; Setting "Verify copies and moves with XXH3" (`Verify=1`, default on) persists in `settings.ini`. Hash runs before deleting the backup/source, so a mismatch restores the old destination, removes the bad copy and keeps the source. Known-answer self-test vectors (empty/abc/240/241/1024/1025/3000 bytes) = `/selftest` exit 0; E2E copy + copy-overwrite + cross-drive move pass (hashes match, no `.sctmp`).

- README.md / README.ar.md / README.fr.md: added the `SuperCopy.exe` SHA-256 (`9890A541…`) under the Download table so users can verify the download.

- Access-denied fallback restored: on a finished job with `AccessDenied` (and not already elevated) the window asks "Retry the failed items as administrator?"; Yes re-enqueues the failed items (`QueueFailed` → `QueueStore`) and starts an elevated instance (`Admin.Relaunch`, UAC `runas`, `/queue` mode = `MainForm(null)` fed by `PollQueue`); `retryEscalated` stops the old instance from eating its own queue file before it exits. Re-adds the self-elevation removed in the AV-hardening pass (AV risk). Verified: build (v3.5 csc) + `/selftest` exit 0; `/queue` opens and stays alive.

- Install now deploys the running exe to `%APPDATA%\SuperCopy\SuperCopy.exe` and points every Explorer verb at it; Remove uninstalls the menu and deletes that copy. New `Installer` class (`Install`/`Remove`), `Paths.ExeInstalled`; `ShellIntegration.IsInstalled` also requires the file; `/install` + `/uninstall` and the Settings buttons route through `Installer`; Settings Save refreshes labels against `Paths.ExeInstalled`. Remove-while-running renames the locked exe to `.old` and `Paths.Init` sweeps it. AV note: this reintroduces the `%APPDATA%` self-copy that was removed for AV reputation. Verified: build (v3.5 csc) + `/selftest` exit 0; `/install` → file + registry command point at the AppData copy; `/uninstall` → file and key gone.

- Installer experiment reverted: it made AV detections worse (Program Files self-copy + HKLM uninstall key + `MoveFileEx` reboot-delete + reflection/COM shortcut = generic `MSIL/Tpyn` heuristics). Back to the portable design: per-user Explorer menu only, `/install` + `/uninstall` are menu add/remove, no `/setup`, no `Installer`/`CheckLocal`/`Admin.Relaunch(args)`.
- Kept the AV-positive changes: `ShellIntegration.Install(exe)` registers the exe path (no `%APPDATA%` self-copy, icon = `exe,0`), `IconMaker.WriteIco`/`Paths.IcoFile` removed, self-update opens the browser, publisher metadata. Settings startup now checks the registry menu (`ShellIntegration.IsInstalled`) instead of an installed exe.
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

- Localization: settings `Language` 0 auto (OS UI culture) / 1 English / 2 Arabic / 3 French; `L` class (`T(en,ar,fr)`, `Rtl`, `Left`/`Right`, `ApplyRtl`); all visible strings in MainForm, Settings, ConflictDialog, tray/Explorer verbs, Program/Updates messages; Settings language combo applies live (`MainForm.ApplyLanguage`).
- Arabic RTL: form-level `RightToLeft` for standard controls; custom paint (HeaderPanel, TitleBar, progress bars, job strip, column headers) uses `L.Rtl`; list columns are physically reordered in RTL (`BuildColumns` + display→logical map) so File is the rightmost column like Explorer; bottom bar and header buttons mirrored.
- Verified: build (v2.0.50727, now `/codepage:65001` in `build.cmd`) + `/selftest` exit 0; visual checks of Settings + MainForm in Arabic and English via `PrintWindow`.
- Limits: `CopyJob.Summary` is built at job creation, so switching language mid-job leaves that label in the old language; native folder pickers follow the OS language, not the app setting.
- README split into `README.md` / `README.ar.md` / `README.fr.md` with badge-style language buttons at the top; the Arabic file is wrapped in `<div dir="rtl">`.
- Settings Save now rewrites the installed Explorer menu (`ShellIntegration.Install(Application.ExecutablePath)` when `IsInstalled()`), so the context-menu labels follow the selected UI language instead of keeping the install-time language.

## AV hardening (2026-10-18)
- Removed the self-elevation path: deleted `Admin.Relaunch` (UAC `runas`), the `/queue` startup mode, `QueueFailed` re-queue and the `retryEscalated` flag. Access-denied jobs now show one informational hint ("restart as administrator") when not elevated; the app never starts another copy of itself.
- Removed the startup network check: `SettingsForm` no longer spawns `StartupUpdateCheck`; deleted `Updates.GetLatest` + `QuickWebClient`. Network access now happens only on a manual "Check for updates" click.
- Removed the kernel32 P/Invoke (`GetCurrentProcess`/`IsWow64Process`): `Bitness` reads `PROCESSOR_ARCHITEW6432`/`PROCESSOR_ARCHITECTURE`.
- Build moved to the .NET Framework 3.5 `csc.exe`, so the explicit `asInvoker` manifest (`app.manifest` + `/win32manifest`) is now embedded; the exe still targets the .NET 2.0 runtime. README (EN/AR/FR) updated to say 3.5 csc for building, 2.0+ to run.
- Kept: HKCU-only Explorer menu, in-process copy engine, optional Robocopy, user32 double-buffer + shell32 `SHChangeNotify` P/Invokes.
- Verified: build OK, `/selftest` exit 0, GUI starts (Settings window stays open, no second process).
- Limits: an unsigned exe can still be flagged generically; only Authenticode signing fixes reputation.

## Known gaps (accepted)
- `/todevice` silently no-ops if a SuperCopy GUI is already open (single-instance mutex path).
- Two items can resolve to the same destination path (incl. `UniqueFile` race).
- Free-space estimate is approximate (conflict=2 keep-both can under-count).
