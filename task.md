# Super Copy — UI redesign

Scope: **UI only**, one file `SuperCopy.cs` (WinForms, GDI+, C# 2 / .NET 2.0, XP–11).
Keep: engine, modes, Explorer menu, settings keys, 3 languages, RTL, no new deps, no images.

## Direction

- **Product.** Explorer shell file copier for Windows; one window shows the transfer.
- **Character.** exact, quiet, mechanical.
- **World.** file manager and disk activity: graphite panels, hairlines, mono figures.
- **Moments.** the transfer bar and the file list — every file gets a thin progress rule.
- **Signature.** one amber rule + right-aligned tabular figures; state is a small dot, never a colored button.

## Audit (every sign found)

| Where | Slop |
|---|---|
| `:1423-1440` | Navy-blue themed surfaces; background reads blue, not neutral |
| `:1436-1437` | Cyan + violet as brand pair |
| `:1486`, `:3556` | Cyan→violet gradients on title bar, header rule, scroll thumb, icon tile |
| `:1556-1603` | Progress bar: gradient fill + white gloss + black text shadow |
| `:1471-1485` | Rainbow bar states (blue, green, red, purple, orange) |
| `:3713-3715`, `:3734`, `:5221-5248` | Every button a different saturated color |
| `:1451-1454` | Tahoma bold everywhere; hierarchy has no size/color ladder |
| `:1417-1439`, `:5071-5458` | Hardcoded odd spacing (9, 14, 46, 104, 118, 26…), no grid |
| `:3749-3781` | Header buttons, bottom bar, job strip = three unaligned action areas |
| `:5044-5065` | Job row bar hardcoded at 206 px from the right edge |
| `:3557-3606` | Custom button paints no focus ring (keyboard users get nothing) |
| `:1608-1630` | Logo = gradient rounded tile with white chevrons (classic AI mark) |
| `:4258-4361` | Header copy: "Speed:", "Total ETA:", "Scanning folders...  N files found", trailing "..." |
| `:4688`, `:3771` | "Co&mact" label |
| `:1569-1575` | Looping marquee gradient |

## Tokens

| Role | Value |
|---|---|
| Bg / Panel / Row | `#131313` / `#1A1A1A` / `#161616` |
| Track | `#0D0D0D` |
| Border | `#2A2A2A` |
| Text / Dim / Muted | `#ECECEC` / `#9C9C9C` / `#6F6F6F` |
| Accent | `#E0A33C` (dark ink `#131313` on it) |
| Ok / Fail / Pause | `#5FB37F` / `#D96A5C` / `#C99A3C` — dots and text only |
| Selected / Hover | `#262626` / `#1E1E1E` |
| Type | UI `Segoe UI 9` (Tahoma fallback), figures `Consolas 9` |
| Weights | Regular + Bold only |
| Radii | controls 8, panels 10, bars = height/2 |
| Grid | 4 px; buttons 32 tall, page pad 16/24, row 26 |

## Tasks

### 1. Spacing
- [x] Rebuild every coordinate on the 4 px grid: header 96, bottom bar 56, row 26, pad 16, gaps 8/12.
- [x] One action row per area: Pause/Cancel/Settings left, "Close when finished" + error text right.
- [x] Settings and Conflict dialogs: 24 label->field gap, 32 field rows, equal field width, section gaps 24.
- [x] Job strip bar: width = client − 240, min 120, right-aligned (no fixed 206).

### 2. Color
- [x] Replace the palette with the tokens above. Navy, cyan, violet, gloss, gradient brushes go.
- [x] Delete `LinearGradientBrush` everywhere (`:1512-1603`, `:3352-3384`, `:3446-3555`, `:4920-4935`, `:1608-1630`).
- [x] Progress fill = accent only; state changes belong to the dot and word. Delete `BarColors` (`:1472`) and `BarBrush` (`:1521`).
- [x] Border hairlines at 8–10% white; selection and hover are one surface step, no tint.
- [x] Drop zebra rows (`:4964`); use a 1 px hairline separator.
- [x] Red/green only on error count, failed rows and status dot.

### 3. Icons and logo
- [x] Redraw the app icon flat: dark square, one solid amber double chevron (`:1608-1630`).
- [x] Wordmark = "Super Copy" in the UI font; no gradient tile in `TitleBar`.
- [x] Scrollbar: plain 4 px thumb, no gradient, no grip lines.
- [x] No icon set is added; text-only buttons stay. No emoji anywhere.

### 4. Copy
- [x] Header = one line: `Copying 3 items` / `Moving 1 folder`; second line = `12.4 MB/s · 1:12 left · 128 / 524 files`.
- [x] `Waiting`, `Scanning · 128 files`, `Completed`, `Cancelled`, `Failed` — no `...`, no double spaces, no `Speed:` / `Total ETA:` prefixes.
- [x] `Co&mact` -> `Compact` / `Files`, no mnemonic.
- [x] Settings label: `Installed · 64-bit` (drop "Status:").
- [x] Every new/changed string gets ar + fr (`L.T(en, ar, fr)`).

### 5. Type
- [x] `Theme.Cell` -> Segoe UI 9; `Title` 11 regular; Bold only for the leading figure.
- [x] Figures (size, speed, %, ETA, counts) in Consolas, one format, right-aligned.
- [x] Remove bold from body cells, status words and column headers (`:4258-4361`, `:4933`, `:4993-5035`).
- [x] No negative tracking, no italics, no uppercase labels.

### 6. Layout
- [x] Header: title left, elapsed right, 8 px bar, one meta line. Hide/Compact become quiet 28 px text buttons in the header's top right.
- [x] Keep window proportions (800×520 content, plus a 36 px title strip; min 660×416).
- [x] Settings window: single column, labels left / fields right, hairline groups: Language, Copy, Behavior, Explorer menu, Version.
- [x] Conflict dialog: file name, existing/incoming meter rows, 4 options at 32 px, safe default still "Skip".

### 7. Details
- [x] FlatButton: neutral surface, hairline, hover/press one step, disabled muted, **1 px accent focus ring**.
- [x] One button style; delete the `Color back` argument from `UI.MakeButton` and all call sites.
- [x] Bar text outside the bar (or inset 12 px), no shadow; radius = height/2.
- [x] List row states readable apart: header (dim mono), pending, active (accent rule), done (dim), failed (red dot).
- [x] Tooltips keep full error text where the row truncates.

### 8. Motion
- [x] Keep only the scanning marquee, neutral accent on track, no gloss.
- [x] No other animation. Timer-driven repaint logic stays as is.

### 9. Chrome and theme
- [x] Each window draws its own strip (36 px main, 64 px Settings, 40 px dialogs): icon, wordmark, theme, minimize, maximize, close. No native caption.
- [x] Drag a window by its strip (native move loop, so edge snapping works); double-click toggles maximize.
- [x] Maximize fills the working area and restores to the exact same bounds; no frame, so no size drift.
- [x] Sun / moon button switches light and dark live; `Theme=` in settings.ini keeps the choice.
- [x] Button borders use the theme's warm line, accent on hover/press.

### 10. Verify
- [x] `build.cmd` compiles with no new warnings; `SuperCopy.exe /selftest` returns 0.
- [ ] Open Settings and the progress window at 100% and 150% DPI; nothing clipped, nothing misaligned.
- [x] RTL (Arabic) still mirrors; every new string translated.
- [x] `grep LinearGradientBrush` = 0; no `Color.FromArgb(` button colors left.
- [x] Contrast: Dim on Panel ≥ 4.5:1, accent on Panel ≥ 4.5:1, accent ink on accent ≥ 4.5:1.

## Not doing

No engine/settings changes beyond the theme key, no generated images, no icon pack, no new fonts bundled.
