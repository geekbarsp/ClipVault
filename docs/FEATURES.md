# Feature implementation ledger

## Implemented and build-verified

- Native WinUI 3/.NET 10 x64 project and solution.
- MVVM state and commands; Microsoft DI composition root.
- SQLite schema, migrations ledger, WAL, foreign keys, indexes, FTS5 triggers, and parameterized queries.
- Text, URL, file-list, and bitmap capture with bounded clipboard-lock retry and maximum item size.
- SHA-256 deduplication, retention protection for pinned items, search, filters, pin/favorite independence, soft deletion, and restore services.
- Main Fluent/Mica dashboard and compact Acrylic window.
- Real Win32 topmost behavior with independent persisted main/compact state.
- Real global hotkey registration and conflict logging.
- Safe paste guard: remembers a foreground target, restores content, revalidates/focuses the same HWND, then sends Ctrl+V; otherwise it falls back to copy only.
- Pause/resume and Private Mode state, local settings, original app icon, single-instance guard, and sensitive-content-free logging.
- Text utilities and safe snippet placeholder expansion in the core library.
- Single-file offline installer containing the self-contained x64 application, with Start Menu/optional desktop shortcuts and uninstall support.

## Implemented but needs interactive Windows verification

- Clipboard capture across target applications and transient clipboard contention.
- Automatic paste behavior in applications with custom input handling.
- Compact window behavior, native title-bar hit testing, topmost restore, and global shortcut conflict UX.
- Image capture payload compatibility across screenshot producers.
- MSIX generation/signing/install/uninstall on a clean machine.

## Not yet implemented

- System tray, close/minimize-to-tray, startup registration, notifications, and single-click tray preferences.
- Rich Text/HTML/custom-format preservation and source-application discovery.
- Tag/collection editing UI, bulk actions, undo, permanent-delete UI, and usage statistics.
- Full snippets UI, per-snippet shortcuts, and placeholder dialog.
- Image editor, thumbnail cache, PNG/JPEG/WebP export, link-opening UI, and text-tool/diff UI.
- Automation-rule editor/runtime, exclusions, optional database encryption/key protection, and private-data export warnings.
- Settings dashboard, configurable shortcuts, theme/accent picker, window-position/DPI recovery, backup/import/export, and localization.
- Authenticode-signed installer, update service, accessibility audit, and automated Win32 integration tests.

The UI does not present the unimplemented sections as working controls.
