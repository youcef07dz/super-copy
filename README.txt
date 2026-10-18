SUPER COPY  -  lightweight copier for Windows XP .. 11  (.NET 2.0, built with csc.exe)
========================================================================================

BUILD
  1. Double-click build.cmd  (uses %WINDIR%\Microsoft.NET\Framework\v2.0.50727\csc.exe)
  2. Result: SuperCopy.exe  (single file, no dependencies)

INSTALL THE MENU
  Run SuperCopy.exe  ->  "Install menu".
  (or:  SuperCopy.exe /install   and   SuperCopy.exe /uninstall)
  Entries go into HKCU\Software\Classes, so no administrator rights are needed.
  A stable copy of the EXE is kept in %APPDATA%\SuperCopy.

USE
   1. Right-click files / folders  ->  Super Copy  >  Super Copy (or Super Cut (move))
   2. Right-click empty space inside the destination folder  ->  Super Paste here
   3. The popup shows:
       - overall progress bar, speed, total ETA, elapsed time, [Hide] button
       - every file with its own progress bar, speed, ETA and status
       - several pastes at once are queued in a strip of per-operation bars
  [Hide] sends the window to the tray; double-click the tray icon to bring it back.

SETTINGS  (button in the popup, or run SuperCopy.exe)
  - Concurrent copies 1..8 (changes apply live while copying)
  - Buffer size, "file exists" behaviour, auto-close
  Stored in %APPDATA%\SuperCopy\settings.ini

NOTES
  - Windows XP has no right-click menu on empty folder background: right-click the
    destination folder instead.
  - Windows 7+ hides static menu entries when more than 100 items are selected:
    copy the parent folder instead.
  - Windows 11 shows the entries under "Show more options" (Shift+F10).
  - Same-drive "Super Cut" is an instant rename; across drives it copies, then deletes.
  - To remove everything: Settings -> "Remove menu", then delete %APPDATA%\SuperCopy.
