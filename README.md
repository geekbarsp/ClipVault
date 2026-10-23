# ClipVault

ClipVault is a native Windows clipboard manager built with C# 14, .NET 10, WinUI 3, Windows App SDK 2.5.1, MVVM, SQLite/FTS5, dependency injection, and Serilog.

## Current working slice

- Event-driven clipboard capture through `AddClipboardFormatListener` / `WM_CLIPBOARDUPDATE`.
- Persistent text, URL, file-list, and bitmap history in local SQLite storage.
- FTS5 search, content hashes, duplicate suppression, retention cleanup, pin, favorite, trash, and restore primitives.
- Dashboard with search, virtualized history list, preview, copy, guarded paste, pin, favorite, delete, monitoring, and private-mode actions.
- Independently topmost main and compact windows using `SetWindowPos(HWND_TOPMOST/HWND_NOTOPMOST)` with separate persisted settings.
- Compact search/copy/paste window opened with `Ctrl+Shift+V`.
- Global shortcuts: `Ctrl+Shift+V` compact mode, `Ctrl+Shift+H` dashboard, `Ctrl+Alt+P` main-window topmost.
- Local-only data, no telemetry, no remote metadata lookup, and logs that exclude clipboard payloads.
- Single-instance guard and rolling diagnostic logs.
- Original ClipVault icon and generated Windows tile assets.

The complete product brief is intentionally larger than the verified slice. See [FEATURES.md](docs/FEATURES.md) for the honest implementation ledger.

## Build and test

Requirements: Windows 10 1809+ or Windows 11, .NET 10 SDK, Developer Mode for command-line launch. The project restores Windows App SDK and build tooling from NuGet.

```powershell
dotnet test .\tests\ClipVault.Tests\ClipVault.Tests.csproj -c Release
dotnet build .\src\ClipVault.App\ClipVault.App.csproj -c Release -p:Platform=x64
dotnet run --project .\src\ClipVault.App\ClipVault.App.csproj -c Debug -p:Platform=x64
```

Or run `./build.ps1`. The verified standalone x64 application is generated at `artifacts/ClipVault-win-x64/ClipVault.App.exe`. Run `./build.ps1 -Installer` to create the offline installer.

## Storage and privacy

Data lives in `%LOCALAPPDATA%\ClipVault`: `clipvault.db`, `settings.json`, image payloads, and redacted diagnostic logs. ClipVault does not upload clipboard contents. Private mode stops capture immediately and does not queue clipboard events. Password detection is not reliable; use excluded-app support once implemented or pause capture before copying secrets.

## Keyboard shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+Shift+V` | Open compact history |
| `Ctrl+Shift+H` | Open the dashboard |
| `Ctrl+Alt+P` | Toggle the main window topmost state |

Windows reports shortcut conflicts; ClipVault records them without replacing reserved shortcuts.

## Installer and packaging

The friend-ready offline installer is generated at `artifacts/installer/ClipVault-Setup-1.0.0-x64.exe`. It includes .NET 10, Windows App SDK, and every native dependency; the target PC does not need to install anything else. It installs per-user without administrator rights, creates a Start Menu shortcut, offers an optional desktop shortcut, and provides an uninstaller.

The complete install → launch → uninstall flow has been tested successfully. The produced installer is approximately 62.3 MiB and has an accompanying SHA-256 checksum file.

The installer is not Authenticode-signed, so Windows may display an unknown-publisher/SmartScreen warning. A trusted commercial code-signing certificate is required to remove that warning safely.

The project also contains a single-project MSIX manifest. `build.ps1 -Package` creates an unsigned MSIX under `src/ClipVault.App/AppPackages` for future Store or enterprise signing workflows.

## Manual verification

Follow [MANUAL-TESTS.md](docs/MANUAL-TESTS.md) before distributing a build.
