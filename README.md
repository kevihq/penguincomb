# Honeycomb

A cross-platform desktop application for creating and managing **Guitar Hero
custom songs**. Honeycomb is a native Avalonia UI port of the classic
Windows-only *Guitar Hero Toolkit* GUI. One codebase builds and runs natively on
**Linux** and **Windows** - no Wine required for Honeycomb itself.

Honeycomb does **not** reimplement any game file formats: all PAK, QB, WAD,
SGH, MIDI/CHART, SKA, audio and checksum work is delegated to the
[GH-Toolkit-NET](https://github.com/AddyMills/GH-Toolkit-NET) library (pinned as
a submodule), the same algorithms used by the original GUI and the
[Honeycomb-CLI](https://github.com/AddyMills/Honeycomb-CLI).

## Features

* Compile songs for **GH3**, **Guitar Hero: Aerosmith**, **GHWT**, **GH5** and
  **GHWoR** (supported consoles differ per game: PC, Xbox 360, PS2, PS3).
* **Batch compile**: queue any number of `.ghproj` projects (or a whole folder)
  and compile them one after another, with per-song status, progress, cancellation
  and a results summary.
* Import SGH archives from GHTCP to PC, or build 360/PS3 packages with Onyx.
* Extract and compile **PAK** files (all consoles; QB files are converted to
  editable Q text and back).
* Extract and compile **PS2 WAD** files.
* Song List Manager: load/delete/export customs, restore the original setlist.
* WTDE (World Tour Definitive Edition) mod output with `song.ini` generation.
* `.ghproj` project files, templates, drag-friendly file pickers and Clone Hero
  folder import.

## Supported games and platforms

| Game | PC | Xbox 360 | PS2 | PS3 |
|---|---|---|---|---|
| Guitar Hero III | ✅ | ✅ | ✅ | ✅ |
| Guitar Hero: Aerosmith | ✅ | ✅ | ✅ | ✅ |
| Guitar Hero World Tour | ✅ (WTDE mods) | – | – | – |
| Guitar Hero 5 / Warriors of Rock | – | ✅ | – | ✅ |

## Installation

### Windows

1. Install the **.NET 8 Desktop Runtime** from
   <https://dotnet.microsoft.com/download/dotnet/8.0> (framework-dependent
   build) - or download a self-contained build and skip this step.
2. Download `honeycomb-win-x64.zip` from the latest release / CI artifacts and
   extract it anywhere.
3. Run `Honeycomb.exe`.
4. The first time you compile a GH3/GHA PC song, Honeycomb asks for your game
   folder (a path containing `GH3.exe` / `Guitar Hero Aerosmith.exe`) and
   creates a backup of the game's QB PAK before touching anything.

### Linux

The easiest way to use Honeycomb on Linux is the **AppImage** - a single,
self-contained file that includes the .NET runtime and every game resource.
No installation, no .NET runtime to install:

```sh
# 1. Download Honeycomb-<version>-x86_64.AppImage from the latest release
# 2. Make it executable and run it
chmod +x Honeycomb-1.0.0-x86_64.AppImage
./Honeycomb-1.0.0-x86_64.AppImage
```

> If your distribution does not provide FUSE (needed to mount AppImages), run
> it with `./Honeycomb-1.0.0-x86_64.AppImage --appimage-extract-and-run` once,
> or install `libfuse2` / use AppImageLauncher.

Prefer a classic layout (or a smaller download)? Use the tar.gz build instead:

```sh
tar -xzf honeycomb-linux-x64.tar.gz
cd honeycomb-linux-x64
./Honeycomb
```

Optional: install the desktop entry and icon:

```sh
install -Dm644 honeycomb.desktop ~/.local/share/applications/honeycomb.desktop
install -Dm644 honeycomb.svg ~/.local/share/icons/honeycomb.svg
```

> **Framework-dependent vs self-contained**: the tar.gz and zip builds are
> framework-dependent and need the .NET 8 runtime. The AppImage is always
> self-contained. `scripts/publish.sh linux-x64 self` produces a self-contained
> tar.gz.

> **Framework-dependent vs self-contained**: framework-dependent builds are
> small (~30 MB) and need the .NET runtime. Self-contained builds include the
> runtime (~80-100 MB) but run anywhere:
> `scripts/publish.sh linux-x64 self`

## Required and optional dependencies

* **ffmpeg + ffprobe** (required for audio compilation; MP3/OGG/FLAC/WAV stems
  are mixed and encoded through ffmpeg). Install via your package manager
  (`ffmpeg` on most distros, `winget install ffmpeg` or the FFmpeg essentials
  build on Windows) or point Honeycomb at a folder containing both binaries in
  **Settings → FFmpeg Folder**.
* **Onyx CLI** (required only for Xbox 360 STFS and PS3 PKG package creation).
  Select the `onyx`/`onyx.exe` executable in **Settings → Onyx CLI Path** when
  prompted. On Linux any executable named `onyx` on your PATH is auto-detected.
* **Wine/Proton** is only relevant for *locating Windows game installations*
  on Linux - Honeycomb itself never runs under Wine.

## Selecting a Guitar Hero installation on Linux

GH3/GHA are Windows-only games. On Linux the game files typically live inside a
Wine or Proton prefix:

* **Wine**: `~/.wine/drive_c/Program Files (x86)/Activision/Guitar Hero III`
* **Steam / Proton**: `~/.local/share/Steam/steamapps/compatdata/<appid>/pfx/drive_c/...`
  (also flatpak `~/.var/app/com.valvesoftware.Steam/...`)

Honeycomb searches these locations automatically and validates the **data
layout** (`DATA/PAK/qb.pak.xen`, `DATA/MUSIC`, `DATA/SONGS`, and
`DATA/patch.pak.xen` for GH3) - it does not require a runnable Windows
executable. If automatic discovery fails, use the manual browse option; the
chosen folder is remembered in Settings.

## Where things are stored

| Purpose | Linux | Windows |
|---|---|---|
| Settings (`settings.json`) | `$XDG_CONFIG_HOME/honeycomb` (default `~/.config/honeycomb`) | `%APPDATA%\Honeycomb` |
| Game QB backups, templates, logs | `$XDG_DATA_HOME/honeycomb` (default `~/.local/share/honeycomb`) | `%APPDATA%\Honeycomb` |
| Cache/temp | `$XDG_CACHE_HOME/honeycomb` (default `~/.cache/honeycomb`) | `%LOCALAPPDATA%\Honeycomb` |
| Toolkit user keys (`keys_user.txt`, `keys_qs_user.txt`) | `$XDG_DATA_HOME/Honeycomb/QBDebug` (default `~/.local/share/Honeycomb/QBDebug`) | `%LOCALAPPDATA%\Honeycomb\QBDebug` |

Game files are only ever modified *after* validation and a one-time backup of
`DATA/PAK/qb.pak.xen` + `qb.pab.xen` into the data directory. The bundled debug
key files (`QBDebug/keys.txt`, `PS2Pak.dbg`) are read-only resources shipped with
the app; user-added keys are written to the per-user folders above so the app
works even when it runs from a read-only location (e.g. an AppImage mount).

On Windows, a one-time migration imports your old `UserPreferences` from the
legacy application on first run.

## Building from source

Prerequisites: .NET SDK 8+, git.

```sh
# 1. Clone (including the pinned GH-Toolkit submodule)
git clone --recursive https://github.com/AddyMills/Honeycomb-GUI.git
cd Honeycomb-GUI

# 2. Apply the GH-Toolkit cross-platform patches (idempotent)
./scripts/init-deps.sh        # Windows: sh scripts/init-deps.sh (git bash)

# 3. Build & test
dotnet restore Honeycomb.sln
dotnet build Honeycomb.sln -c Release
dotnet test Honeycomb.sln -c Release

# 4. Run
dotnet run --project src/Honeycomb.App
```

### Notes on the GH-Toolkit dependency

`external/GH-Toolkit` is a git submodule pinned to a tested commit. A small,
documented patch set (`patches/gh-toolkit-crossplatform.patch`) is applied by
`scripts/init-deps.sh` and makes the toolkit build/run on Linux:

* replaces the out-of-tree FFMpegCore project reference with the official
  `FFMpegCore` NuGet package,
* removes dead `NAudio.Lame`/`Instances` references,
* fixes hardcoded backslash resource paths,
* replaces the Windows-only `MediaFoundationResampler` with a managed
  resampler,
* routes the Onyx and ffprobe launches through safe `ArgumentList` invocation,
* normalizes archive-internal entry paths to backslash form on all platforms.

Do not commit large vendored copies of GH-Toolkit; update the submodule pin +
patches instead.

## Publishing a release

```sh
./scripts/publish.sh linux-x64          # framework-dependent .tar.gz
./scripts/publish.sh win-x64            # framework-dependent .zip/.tar.gz
./scripts/publish.sh linux-x64 self     # self-contained
./scripts/publish.sh linux-arm64        # framework-dependent (ffmpeg must be arm64)
./scripts/build-appimage.sh             # single-file self-contained AppImage
```

Outputs land in `artifacts/`. The AppImage (`Honeycomb-<version>-x86_64.AppImage`)
is the recommended distribution for end users: one file, no dependencies to
install. The Linux tarball includes the binary, the `honeycomb.desktop` entry
and the SVG icon.

CI (GitHub Actions) restores, builds, tests, publishes `linux-x64` and
`win-x64` and uploads both as artifacts on every push, and fails when
WinForms/Registry references leak into shared projects or when resource casing
breaks.

## Known limitations

* Audio compilation requires external `ffmpeg`/`ffprobe` (same as the original
  toolkit; see above).
* PS2 MSV resampling now uses a managed resampler instead of Windows Media
  Foundation; output format/sample rate are identical, sample values may differ
  inaudibly.
* Xbox 360 / PS3 package creation requires Onyx.
* GH5/GHWoR PC compilation is not offered (as in the original tool); those
  games target consoles.
* Registry-based auto-discovery on Windows is best-effort; browse works always.

## Reporting a platform-specific bug

Please open an issue with:

1. Your OS/distribution and desktop environment,
2. Honeycomb version (Help → About shows the version; also printed in the log),
3. The relevant section of the log file (see the storage table above,
   `Logs/honeycomb.log`),
4. Steps to reproduce, and whether the same action works on the other platform.

## License and attribution

* Honeycomb is **GPL-3.0** (see `LICENSE`). Original project and branding by
  **AddyMills** (`Honeycomb-GUI`, `GH-Toolkit-NET`, `Honeycomb-CLI`).
* The GUI port is derived from `Honeycomb-GUI` and keeps its feature set and
  project file format.

Significant dependencies and their licenses:

| Dependency | License |
|---|---|
| Avalonia UI | MIT |
| CommunityToolkit.Mvvm | MIT |
| Microsoft.Extensions.DependencyInjection | MIT |
| GH-Toolkit-NET (submodule) | GPL-3.0 |
| FFMpegCore | MIT |
| NAudio | MIT |
| Melanchall.DryWetMidi | MIT |
| Newtonsoft.Json | MIT |
| ini-parser | MIT |
| SharpZipLib | MIT |
| Ude.NetStandard | MIT |
| YamlDotNet | MIT |
| Microsoft.Win32.Registry (Windows-only) | MIT |
