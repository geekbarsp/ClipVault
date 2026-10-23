# ClipVault installer

`ClipVault.iss` packages the complete self-contained `win-x64` publish directory into one offline installer. The target computer does not need .NET, Windows App SDK, Visual Studio, Developer Mode, or an internet connection.

Run `./build.ps1 -Installer` from the repository root. The result is written to `artifacts/installer/ClipVault-Setup-1.0.0-x64.exe`.

The installer defaults to a per-user location under `%LOCALAPPDATA%\Programs\ClipVault`, creates a Start Menu shortcut, optionally creates a desktop shortcut, and registers a normal Windows uninstaller. Application data under `%LOCALAPPDATA%\ClipVault` is retained on uninstall to prevent accidental loss of clipboard history.

The generated installer is not Authenticode-signed. Windows SmartScreen may therefore show an unknown-publisher warning when it is shared. Production distribution should use a trusted code-signing certificate; do not distribute a private signing key with the source or installer.

## Verified installation flow

The generated installer was tested with a clean, isolated per-user destination: installation returned exit code 0, the installed self-contained application remained running during the startup smoke test, the generated uninstaller returned exit code 0, the program directory was removed, and `%LOCALAPPDATA%\ClipVault` user data was retained.
