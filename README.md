# Honeycomb 🎸

**Play your favorite songs in Guitar Hero 3 — even if they were made for Clone Hero.**

Honeycomb is a free, easy-to-use desktop program (for **Linux** and **Windows**)
that turns your song files into custom Guitar Hero songs. No programming, no
complicated setup — if you can pick two folders and press a button, you can use
Honeycomb.

Created by **Kelvin Klein**.

> **Not a programmer? You're in the right place.** Everything you need is in
> this first half of the page. The detailed documentation for developers and
> contributors is at the bottom.

---

## What is Honeycomb?

Guitar Hero games only let you play the songs that came with them — unless you
add your own. Honeycomb is the tool for that. It takes songs you already have
(like your Clone Hero collection) and turns them into songs your Guitar Hero 3
installation can play.

Some examples of what you can do:

* **Bring your Clone Hero songs to GH3** — pick your song folders, pick your
  GH3 game folder, press Convert. Done. (One button: **"Clone Hero to Better
  GH3"** on the main screen.)
* **Convert many songs at once** — queue up a whole folder of songs and let
  Honeycomb work through them one by one, telling you which ones worked and
  which ones didn't.
* **Build songs from scratch** — write your own charts and compile them into
  playable Guitar Hero songs (GH3, World Tour, and more).
* **Organize your customs** — a song manager, PAK/WAD archive tools, and
  importing shared song packs (`.sgh`).

Supported games: **Guitar Hero III**, **Guitar Hero: Aerosmith**, **World Tour
(Wii, Xbox 360, PS3)**, **GH5 / Warriors of Rock** — see the detailed section
below for the full platform table.

## Download

| Platform | What to download | How to run |
|---|---|---|
| **Linux** | `Honeycomb-<version>-x86_64.AppImage` | Download, `chmod +x`, double-click or run — no installation, no .NET, nothing else to install |
| **Windows** | `honeycomb-win-x64.zip` | Unzip anywhere, run `Honeycomb.exe` (install the **.NET 8 Desktop Runtime** once if asked) |

> **Linux, no FUSE?** If the AppImage doesn't start, run it once with
> `./Honeycomb-1.0.0-x86_64.AppImage --appimage-extract-and-run` or install
> `libfuse2`. That's the whole fix.

## Quick start — Clone Hero to GH3 in 5 minutes

1. **Open Honeycomb.**
2. Click **"Clone Hero to Better GH3"**.
3. Click **Add Songs...** and pick the Clone Hero song folders you want
   (or **Add Library Folder...** to add a whole folder of songs at once).
4. In the **GH3 folder** box, click **Browse...** and select your Guitar Hero 3
   game folder. *(It remembers this, so you only do it once.)*
5. Press **Convert to Better GH3**. That's it.

Your songs will appear in Guitar Hero 3 the next time you start it.

> **Where is my GH3 folder on Linux?** GH3 is a Windows game, so on Linux the
> game files live inside Wine or Proton — for example
> `~/.local/share/Steam/steamapps/compatdata/<appid>/pfx/drive_c/...` or
> `~/.wine/drive_c/...`. Honeycomb finds and checks it for you automatically,
> and it's fine if it's in a Wine/Proton folder — Honeycomb itself never needs
> Wine to run.

## One-time setup: ffmpeg (for audio)

Making a song involves mixing its audio tracks. Honeycomb uses **ffmpeg** for
that — a free, well-known audio tool. Install it once:

* **Linux:** `sudo apt install ffmpeg` (or your distro's package manager)
* **Windows:** `winget install ffmpeg` (or download the FFmpeg "essentials"
  build and point Honeycomb at it in **Settings → FFmpeg Folder**)

## Where does Honeycomb keep its files?

| What | Linux | Windows |
|---|---|---|
| Settings | `~/.config/honeycomb` | `%APPDATA%\Honeycomb` |
| Backups, logs, imports | `~/.local/share/honeycomb` | `%APPDATA%\Honeycomb` |
| Cache / temp | `~/.cache/honeycomb` | `%LOCALAPPDATA%\Honeycomb` |

**Honeycomb never touches your game files without asking first.** Before the
first compile it checks your game folder is correct and makes a backup of the
game's `qb.pak` files — so nothing can go wrong that you can't undo.

## Having trouble?

Open an issue on GitHub (the "Issues" tab) and tell us:

1. Your system (Windows, or Linux distro + desktop environment),
2. What you were doing when it went wrong,
3. The log file — it's in the "Backups, logs, imports" folder above,
   `Logs/honeycomb.log`. It tells us exactly what happened.

We read every issue. Please don't be shy about asking.

---

# For developers and contributors

This half is for people who want to build, extend or contribute to Honeycomb.

## What Honeycomb is, technically

A cross-platform desktop application for creating and managing Guitar Hero
custom songs. Honeycomb is a native **Avalonia UI** port of the classic
Windows-only *Guitar Hero Toolkit* GUI. One .NET 8 / C# codebase builds and
runs natively on **Linux** and **Windows** — no Wine required for Honeycomb
itself.

Honeycomb does **not** reimplement any game file formats: all PAK, QB, WAD,
SGH, MIDI/CHART, SKA, audio and checksum work is delegated to the
[GH-Toolkit-NET](https://github.com/AddyMills/GH-Toolkit-NET) library (pinned as
a submodule), the same algorithms used by the original GUI and the
[Honeycomb-CLI](https://github.com/AddyMills/Honeycomb-CLI).

## Architecture

| Project | Responsibility |
|---|---|
| `src/Honeycomb.App` | Avalonia UI: views, view models, startup, styles, assets |
| `src/Honeycomb.Application` | Use cases: song compilation, SGH import, PAK/WAD, batch + Clone Hero flows, validation |
| `src/Honeycomb.Infrastructure` | Settings (JSON), dialogs, process execution, game/tool locators, per-user directories |
| `external/GH-Toolkit` | Pinned submodule: file-format and compilation algorithms |
| `tests/Honeycomb.Tests` | Unit + integration + headless Avalonia smoke tests |

Platform-specific behavior goes behind interfaces
(`IPlatformService`, `ISettingsService`, `IFileDialogService`,
`IExternalProcessService`, `IGameInstallLocator`, `IExternalToolLocator`);
Windows-only implementations live in `Honeycomb.Infrastructure` and are guarded
by runtime platform checks. No shared project references WinForms or the
Windows Registry.

## Features

* Compile songs for **GH3**, **Guitar Hero: Aerosmith**, **GHWT**, **GH5** and
  **GHWoR** (supported consoles differ per game: PC, Xbox 360, PS2, PS3).
* **Batch compile**: queue any number of `.ghproj` projects **or Clone Hero song
  folders** and compile them one after another, with per-song status, progress,
  cancellation and a results summary. An optional song-name suffix tags imported
  songs with their source (e.g. `My Song - GH 2`).
* **Clone Hero to Better GH3** (main window): the quick one-shot flow — pick
  Clone Hero song folders, point at the GH3 game folder (remembered in
  settings), press Convert. No project files are created or saved.
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

## Installing from a build (framework-dependent vs self-contained)

* The **tar.gz** and **zip** builds are framework-dependent (~30 MB) and need
  the .NET 8 runtime installed.
* The **AppImage** is always self-contained (includes the runtime).
* `./scripts/publish.sh linux-x64 self` produces a self-contained tar.gz.

## Required and optional dependencies

* **ffmpeg + ffprobe** (required for audio compilation; MP3/OGG/FLAC/WAV stems
  are mixed and encoded through ffmpeg). Install via your package manager or
  point Honeycomb at a folder containing both binaries in
  **Settings → FFmpeg Folder**.
* **Onyx CLI** (required only for Xbox 360 STFS and PS3 PKG package creation).
  Select the `onyx`/`onyx.exe` executable in **Settings → Onyx CLI Path** when
  prompted. On Linux any executable named `onyx` on your PATH is auto-detected.
* **Wine/Proton** is only relevant for *locating Windows game installations*
  on Linux — Honeycomb itself never runs under Wine.

## Selecting a Guitar Hero installation on Linux

GH3/GHA are Windows-only games. On Linux the game files typically live inside a
Wine or Proton prefix:

* **Wine**: `~/.wine/drive_c/Program Files (x86)/Activision/Guitar Hero III`
* **Steam / Proton**:
  `~/.local/share/Steam/steamapps/compatdata/<appid>/pfx/drive_c/...`
  (also flatpak `~/.var/app/com.valvesoftware.Steam/...`)

Honeycomb searches these locations automatically and validates the **data
layout** (`DATA/PAK/qb.pak.xen`, `DATA/MUSIC`, `DATA/SONGS`, and
`DATA/patch.pak.xen` for GH3) — it does not require a runnable Windows
executable. If automatic discovery fails, use the manual browse option; the
chosen folder is remembered in Settings.

## Where things are stored (developer detail)

| Purpose | Linux | Windows |
|---|---|---|
| Settings (`settings.json`) | `$XDG_CONFIG_HOME/honeycomb` (default `~/.config/honeycomb`) | `%APPDATA%\Honeycomb` |
| Game QB backups, templates, logs | `$XDG_DATA_HOME/honeycomb` (default `~/.local/share/honeycomb`) | `%APPDATA%\Honeycomb` |
| Cache/temp | `$XDG_CACHE_HOME/honeycomb` (default `~/.cache/honeycomb`) | `%LOCALAPPDATA%\Honeycomb` |
| Toolkit user keys (`keys_user.txt`, `keys_qs_user.txt`) | `$XDG_DATA_HOME/Honeycomb/QBDebug` (default `~/.local/share/Honeycomb/QBDebug`) | `%LOCALAPPDATA%\Honeycomb\QBDebug` |

Game files are only ever modified *after* validation and a one-time backup of
`DATA/PAK/qb.pak.xen` + `qb.pab.xen` into the data directory. The bundled debug
key files (`QBDebug/keys.txt`, `PS2Pak.dbg`) are read-only resources shipped
with the app; user-added keys are written to the per-user folders above so the
app works even when it runs from a read-only location (e.g. an AppImage mount).

On Windows, a one-time migration imports your old `UserPreferences` from the
legacy application on first run.

## Building from source

Prerequisites: .NET SDK 8+, git.

```sh
# 1. Clone this repository (including the pinned GH-Toolkit submodule).
#    Replace the URL below with your fork when contributing.
git clone --recursive https://github.com/your-name/honeycomb.git
cd honeycomb

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

### GitHub Releases

Push a `v*` tag and the `release` workflow builds everything and attaches it to
the **GitHub Release** page — the Linux AppImage, the Linux tarball and the
Windows archive:

```sh
git tag v1.0.0
git push origin v1.0.0
```

The **AppImage** (`Honeycomb-<version>-x86_64.AppImage`) is the single Linux
file to link on the release page: download, `chmod +x`, run — no .NET or FUSE
required (it self-extracts with `--appimage-extract-and-run` when FUSE is
missing).

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
2. Honeycomb version (shown on the main window and printed in the log),
3. The relevant section of the log file (see the storage table above,
   `Logs/honeycomb.log`),
4. Steps to reproduce, and whether the same action works on the other platform.

## License and attribution

* Honeycomb is **GPL-3.0** (see `LICENSE`).
* This project is a **cross-platform port of Honeycomb-GUI** by **AddyMills**
  (GPL-3.0). Original project, branding, workflows and the core file-format
  algorithms belong to AddyMills (`Honeycomb-GUI`, `GH-Toolkit-NET`,
  `Honeycomb-CLI`); see the upstream repositories.
* The GH-Toolkit core is consumed as a **pinned git submodule** with a small
  documented cross-platform patch set (`patches/`), never vendored or forked.
* `legacy/Honeycomb-GUI` contains the original WinForms source for reference
  (GPL-3.0, with its own LICENSE file).
* The GUI port is derived from `Honeycomb-GUI` and keeps its feature set and
  project file format.
* The cross-platform port was created by **Kelvin Klein**.

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
