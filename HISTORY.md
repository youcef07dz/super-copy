# History

## v4 (2026-10-22)

### What's new

- **The file list has its own scrollbars now**: vertical and horizontal bars in the window's design (dark track, rounded gradient thumb with grip lines, highlighted while hovered or dragged), instead of the Windows-themed ones. Click the track to jump, drag the thumb, or use the wheel; they appear only when that direction really scrolls and they mirror correctly in Arabic.
- **Verification costs about half of what it did**: the XXH3 check (on by default) used to be the most expensive part of any copy. It now reads the file in 64 KB chunks, reads its words in one go, keeps the secret table pre-read and reuses its buffers per thread - a 1.6 GB copy went from ~10 s of CPU to ~5.4 s, and the copy finished in ~9 s instead of ~12.5 s.
- The source is **hashed while it is copied** (the verify step then only re-reads the destination), so a verified copy needs one pass over the data instead of two. The streamed hash is checked against the one-shot hash at every block and stripe boundary by `/selftest`.
- **The list stays smooth with thousands of files**: rows are repainted selectively (only copying rows and state changes), the progress-bar gradient brush is cached instead of rebuilt per repaint, the scan spawns up to four workers per tick so a queue of small files does not stall, and robocopy only runs for files of 1 MB or more (smaller files never pay its process start cost).
- **Native look-and-feel fixed in the file list**: the control's own hot/selection colors no longer leak over the rows; a row under the mouse gets a designed subtle tint, a clicked row the stronger selection tint (verified by sampling the actual pixels).
- **DPI aware**: the app declares `dpiAware` in its manifest; the windows scale cleanly at 125-200% text scaling (list columns, row heights and scrollbar metrics included).
- **Buttons redraw themselves**: one flat-button implementation paints hover, pressed and disabled states itself, so the disabled gray-and-embossed text is gone - disabled buttons keep their color with the same white text at half strength. Keyboard mnemonics (Alt + underlined... keyboard letters) work on all buttons in all languages.
- **File-list right-click menu**: open the destination folder (name), copy the source/destination path, and retry the failed items of the queue.
- **"Check for updates" no longer freezes the settings window**: the request runs in the background with a 10 s timeout; the button shows "Checking..." until it answers.
- Double-clicking a row in the file list opens the copied file's folder; double-clicking a queue entry opens that job's destination.
- The conflict prompt shows the size and last-write time of both files, so "overwrite if newer" is a real decision.
- When a job finishes with errors while the window is hidden, a tray balloon announces it instead of pulling the window over whatever you are doing; the full last error is always on a tooltip.
- TLS 1.2 is only added to the update check's protocols, never replaces them.
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

### Archive

The v3 source and binary are kept in [`old/v3`](old/v3).

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
