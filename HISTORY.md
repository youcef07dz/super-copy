# History

## v3 (2026-10-21)

### What's new

- **Super Copy can be the default copy**: press `Ctrl+V` in a folder window and the paste is done by Super Copy — same engine, same conflict rules, XXH3-64 verify, progress window — instead of the shell's own copy. The shell paste is suppressed, so there is no double copy.
- New Settings option **"Use Super Copy as the default copy"** (`DefaultCopy=1` in `settings.ini`), enabled by default.
- A small background helper (`SuperCopy.exe /agent`) does the work: it watches `Ctrl+V` only while an Explorer window has the focus, reads the file list and the cut/copy flag from the clipboard, finds the destination folder through the shell, and hands the job to Super Copy.
- The helper **starts with Windows** while the option is on (per-user entry `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`) and stops itself when the option is switched off.
- The copy window now **comes on top** while the job runs, and the header shows **two progress bars**: one for the whole operation and one for the file being copied (same size, in full and in compact mode).
- Still pure .NET 2.0 managed code: runs on Windows XP SP3 through Windows 11, 32-bit and 64-bit.

### Archive

The v2 source and binary are kept in [`old/v2`](old/v2) (v1 in [`old/v1`](old/v1)).

## v2 (2026-10-20)

### What's new

- **XXH3-64 integrity verification** after every copy and move, with both the built-in and Robocopy engines.
- A mismatch removes the bad destination, restores the previous destination when one was overwritten, and always keeps the source.
- New Settings option **"Verify copies and moves with XXH3"**, enabled by default and stored as `Verify=1` in `settings.ini`.
- Still pure .NET 2.0 managed code: runs on Windows XP SP3 through Windows 11, 32-bit and 64-bit.

### Archive

The previous v1 source and binary are kept in [`old/v1`](old/v1).

## v1 (2026-10-18)

Initial public release: Explorer context menu, multi-threaded copy and move, queue, drag and drop, English/Arabic/French UI, built-in and Robocopy engines, error report, and update check.
