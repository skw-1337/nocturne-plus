# Nocturne Plus

Launcher for **Nocturne** (Terminal Reality, 1999) on Windows 10/11, in the spirit of
[Terra Nova Plus](https://github.com/skw-1337/terra-nova-plus): one small window, one **Launch** button.

- **Ultra HD**: real hardware 3D through dgVoodoo 2 (Direct3D 11), rendered at your screen's height,
  native **16:9** option, 16x anisotropic filtering, 60 FPS (the cape physics follows the frame rate).
- **Stable**: software rendering through cnc-ddraw, the safe fallback.
- **Engine fixes** applied to `nocturne.exe`, byte-checked and reversible (original kept):
  precise engine clock (fixes the multi-core freeze and slow menus), no false low-memory warning,
  widescreen camera, loose files override the `.POD` archives.
- **AI-remastered assets** (optional, generated locally from your own game files with Real-ESRGAN):
  pre-rendered backgrounds cleaned of 256-colour grain, 3D textures enlarged to 256x256.
- Borderless window with working Alt+Tab, freeze watchdog with per-thread diagnostics, update check.

The game files are **not** included: you need your own copy of Nocturne.

## Layout

| Path | Content |
|---|---|
| `NocturnePlus/src/` | C# 5 sources (.NET Framework 4, built with the `csc` shipped with Windows) |
| `NocturnePlus/compiler.bat` | build script (x86) |
| `NocturnePlus/outils/` | Python scripts that generate the HD backgrounds and textures |
| `NocturnePlus/NOTICE.md` | user guide (French) |
| `NocturnePlus/RAPPORT.md` | technical report: causes found, patches, measurements (French) |

## Release

Bump `AssemblyVersion` in `NocturnePlus/src/Version.cs`, run `compiler.bat`, then publish a GitHub
release tagged `vX.Y.Z` with `NocturnePlus.exe` attached. Older launchers then show "Update available".
