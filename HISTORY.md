# History

## Unreleased

### What's new

- **Safety nets around the copy engine**: a file whose source disappears between the scan and the copy is reported as an error instead of being skipped silently; a destination that cannot be set aside is never overwritten in place; a failed or incomplete copy always puts the previous destination back and keeps the source; the length of the copy is checked even when the hash verification is switched off.
- Two files that end up at the same destination can no longer be written at the same time.
- A crash in the scan or in a copy worker no longer ends the whole program: the job reports the error and the other workers keep going.
- The "ask first" dialog cannot be answered by accident: the safe choice (skip) is preselected, the dialog stays on top, and cancelling it skips the other waiting files too.
- The queue hands a job over by claiming the file first, so two windows can never run the same job.
- `settings.ini` is written atomically now: a half-written file can no longer switch the background helper off by accident.
- The background helper only takes over `Ctrl+V` when the clipboard really holds files, reads the cut/copy flag from locked memory and no longer cuts long paths short.
- The window closes without blocking the interface: the workers are cancelled and the window waits for them in the background instead of freezing the program for three seconds.
- Pause and Cancel always act on the job that is really running; "Reset to default" in the settings no longer changes anything when the window is cancelled.
- **Less idle work**: the window no longer repaints itself four times a second when nothing changes. The header is redrawn only when a value really changes (or while the scan marquee animates), the file list only while a job runs, when a file is added or when the last job ends.
- The background helper no longer re-reads `settings.ini` every two seconds; it only reads the file when it actually changed.
- The queue is polled once a second instead of twice.
- The keyboard hook only looks at the Ctrl/Alt keys for the `V` keypress instead of for every key of the whole system.
- The progress bar keeps one shared border pen instead of creating a new one on every repaint.
- Result: an idle Super Copy window uses no measurable CPU any more (was ~0.3% of a core).

## v3 (2026-10-21)

### What's new

- **Super Copy can be the default copy**: press `Ctrl+V` in a folder window and the paste is done by Super Copy — same engine, same conflict rules, XXH3-64 verify, progress window — instead of the shell's own copy. The shell paste is suppressed, so there is no double copy.
- New Settings option **"Use Super Copy as the default copy"** (`DefaultCopy=1` in `settings.ini`), enabled by default.
- A small background helper (`SuperCopy.exe /agent`) does the work: it watches `Ctrl+V` only while an Explorer window has the focus, reads the file list and the cut/copy flag from the clipboard, finds the destination folder through the shell, and hands the job to Super Copy.
- The helper **starts with Windows** while the option is on (per-user entry `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`) and stops itself when the option is switched off.
- The copy window now **comes on top** while the job runs, and the header shows the progress of the whole queue: every file added to the queue is listed right away (the folders are scanned as soon as the job is queued) and the total size, the progress bar, the average speed and the ETA cover all the queued jobs, not only the one that is running.
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
