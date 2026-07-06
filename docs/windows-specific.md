# Windows-specific

OpenGameHUB is built as a Windows desktop application. This document lists platform dependencies and their reasons.

## Project and runtime

| Element | Value |
|---------|-------|
| TFM | `net8.0-windows` |
| Output | `WinExe` |
| Manifest | `app.manifest` — Windows 10+ compatibility |
| Entry | `[STAThread]` in `Program.cs` — traditional Windows COM/UI |

**Why not cross-platform `net8.0` yet:** Registry, Xbox Appx, `C:\` paths, Inno installer, and legendary `.exe` are integrated into the main flow.

## Registry (`Microsoft.Win32`)

Used to locate launchers and data without hardcoding a single path:

| Area | Typical file |
|------|--------------|
| Steam install path | `SteamLocalAccountReader`, `GameLibraryService` |
| Epic Launcher | `LegendaryClient.FindEpicLauncherExecutable` |
| EA / Origin | `EaDesktopScanner`, `EaCatalogReader` |
| Ubisoft | Indirect via GameLib + `%LocalAppData%` paths |

If registry has no entry, default paths under `Program Files` are tried.

## Common absolute paths

```
C:\Program Files (x86)\Steam\steam.exe
C:\Program Files (x86)\Epic Games\Launcher\...
C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests\
C:\Program Files\Electronic Arts\EA Desktop\...
%LocalAppData%\Ubisoft Game Launcher\cache\...
%LocalAppData%\Packages\...\AppxManifest.xml  (Xbox)
```

**Why hardcode + registry:** PC launchers do not follow a single install standard; this is the pattern used by GameLib and similar tools.

## Processes and protocols

| Mechanism | Use |
|-----------|-----|
| `Process.Start` + `UseShellExecute=true` | `steam://`, `com.epicgames.launcher://`, `uplay://` |
| `Process.Start` + `CreateNoWindow` | legendary auth/list (no console window) |
| `cmd.exe /c start` | Fallback if protocol not registered |
| `LegendaryClient.IsLegendaryExecutable` | Hide window when launching via `launcher-args` with legendary |

Windows registers protocol handlers when each launcher is installed.

## Xbox / Microsoft Store

- Appx packages in `WindowsApps` (protected folder)
- `GameFinder.StoreHandlers.Xbox` for metadata
- PowerShell `Get-AppxPackage` in some cases
- `shell:AppsFolder\...` URIs

Only makes sense on Windows 10/11 with Xbox App / Gaming App installed.

## EA encryption

`EaInstallInfoDecryptor`:

- `System.Management` — WMI `Win32_VideoController` for PNPDeviceId
- GameFinder `EADesktopHandler.DecryptInstallInfoFile` — EA-specific encryption logic on Windows

This code would not compile/run on Linux without a full replacement.

## Installer

- **Inno Setup 6** — `installer/OpenGameHUB.iss`
- Output: `dist/OpenGameHUB-Setup-{version}.exe`
- Per-user install (`PrivilegesRequired=lowest`)

Build script: `build-installer.ps1` (PowerShell + `dotnet publish` + ISCC).

## legendary on Windows

- Binary: `legendary.exe` (upstream PyInstaller)
- Auto-download to `%LocalAppData%\OpenGameHUB\tools\`
- Credentials: `%USERPROFILE%\.config\legendary\`

## What would be needed for Linux (reference)

Not implemented; for orientation:

| Windows piece | Linux equivalent |
|---------------|------------------|
| Registry | `.desktop`, `~/.steam`, FHS paths |
| `legendary.exe` | `legendary` on PATH (pip) |
| Epic protocol | legendary launch / Heroic patterns |
| Inno Setup | .deb / AppImage / flatpak |
| Xbox scanner | Remove or replace |
| EA decrypt | No direct equivalent |

See README: Linux not supported for now.
