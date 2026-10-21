# Nocturne Plus

Launcher for **Nocturne** (Terminal Reality, 1999) on Windows 10/11, in the spirit of
[Terra Nova Plus](https://github.com/skw-1337/terra-nova-plus): one small window, one **Launch** button.

- **Ultra HD**: real hardware 3D through dgVoodoo 2 (Direct3D 11), rendered at your screen's height,
  native **16:9** option, 16x anisotropic filtering, 60 FPS (the cape physics follows the frame rate).
- **Stable**: software rendering through cnc-ddraw, the safe fallback.
- **Engine fixes** applied to `nocturne.exe`, byte-checked and reversible (original kept):
  precise engine clock (fixes the multi-core freeze and slow menus), no false low-memory warning,
  widescreen camera, loose files override the `.POD` archives.
- **HD pack** (optional, one button): pre-rendered backgrounds cleaned of 256-colour grain and 3D
  textures enlarged to 256x256, generated locally from *your own* game files with Real-ESRGAN.
- Borderless window with working Alt+Tab, freeze watchdog with per-thread diagnostics, update check.

## Install

Download `NocturnePlus.exe` from the [latest release](https://github.com/skw-1337/nocturne-plus/releases/latest),
put it in your Nocturne folder (next to `nocturne.exe`) and run it. **Never as administrator.**
Nothing else to download by hand:

| Component | How it gets there |
|---|---|
| cnc-ddraw 7.1 (Stable mode) | bundled inside `NocturnePlus.exe` (MIT licence) |
| dgVoodoo 2.87.5 (Ultra HD mode) | downloaded on first Ultra HD launch from the author's official release |
| Real-ESRGAN (HD pack) | downloaded only when you click *Generate HD pack* |

Downloads are checked against a pinned SHA-256. The game files are **not** included: you need your own
copy of Nocturne, and the HD pack is never distributed (it is derived from the game's assets).

## Credits

- **dgVoodoo 2** by Dege — <http://dege.fw.hu> · <https://github.com/dege-diosg/dgVoodoo2> (not redistributed)
- **cnc-ddraw** by FunkyFr3sh — <https://github.com/FunkyFr3sh/cnc-ddraw> (MIT, see `NocturnePlus/tiers/cnc-ddraw/LICENSE.txt`);
  `lanczos2-sharp.glsl` by Hyllian (MIT, notice inside the file)
- **Real-ESRGAN** by Xintao Wang et al. — <https://github.com/xinntao/Real-ESRGAN> (BSD-3-Clause, not redistributed)

## Layout

| Path | Content |
|---|---|
| `NocturnePlus/src/` | C# 5 sources (.NET Framework 4, built with the `csc` shipped with Windows) |
| `NocturnePlus/tiers/cnc-ddraw/` | cnc-ddraw files embedded in the exe, with their licence |
| `NocturnePlus/compiler.bat` | build script (x86) |
| `NocturnePlus/outils/` | original Python versions of the HD pack scripts, and the icon generator |
| `NocturnePlus/NOTICE.md` | user guide (French) |
| `NocturnePlus/RAPPORT.md` | technical report: causes found, patches, measurements (French) |

## Release

Bump `AssemblyVersion` in `NocturnePlus/src/Version.cs`, run `compiler.bat`, then publish a GitHub
release tagged `vX.Y.Z` with `NocturnePlus.exe` attached. Older launchers then show "Update available".
