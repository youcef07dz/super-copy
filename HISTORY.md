# History

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
