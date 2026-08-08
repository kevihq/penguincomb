# Porting notes: Honeycomb WinForms -> Avalonia

This document describes how the original Windows-only WinForms application
(`Honeycomb-GUI`) was ported to the cross-platform Avalonia application in this
repository, what changed, and how the two relate.

## 1. Current architecture

```
┌────────────────────────────────────────────────────────────┐
│ Honeycomb.App (Avalonia UI, net8.0)                        │
│   Views (AXAML)  ·  ViewModels (CommunityToolkit.Mvvm)     │
│   App / Program (DI composition) · dialogs · console sink  │
├────────────────────────────────────────────────────────────┤
│ Honeycomb.Application (net8.0, no UI, no platform APIs)    │
│   Abstractions (IPlatformService, ISettingsService,        │
│   IFileDialogService, IExternalProcessService,             │
│   IGameInstallLocator, IExternalToolLocator, ...)          │
│   Services: SongCompileService, SghImportService,          │
│   PakToolService, WadToolService, SongListService,         │
│   PreCompileChecks, ProjectFileService, GameInstallValidator│
│   Models: SongProjectData (ghproj), AppSettings, ...       │
├────────────────────────────────────────────────────────────┤
│ Honeycomb.Infrastructure (net8.0)                          │
│   JsonSettingsService · ExternalProcessService             │
│   AppDataLocator (XDG / %APPDATA%)                         │
│   WindowsGameInstallLocator (registry, guarded)            │
│   LinuxGameInstallLocator (Wine/Proton prefix discovery)   │
│   ExternalToolLocator (onyx/ffmpeg) · FilePermissionService│
├────────────────────────────────────────────────────────────┤
│ external/GH-Toolkit (pinned git submodule + patches)       │
│   All file-format and compilation algorithms (referenced)  │
└────────────────────────────────────────────────────────────┘
```

* The UI never touches filesystem, settings, processes or game files directly;
  it only calls the services above.
* The heavy lifting (PAK/QB/WAD/SGH/MIDI/SKA/audio) is done by the referenced
  GH-Toolkit library, exactly like the old GUI and the CLI.
* `Honeycomb.Tests` contains unit + integration tests plus Avalonia headless
  smoke tests.

## 2. Windows-specific dependencies (old GUI)

| Dependency | Where it was used | Replacement |
|---|---|---|
| `System.Windows.Forms` (WinForms) | every form/designer | Avalonia views (AXAML) |
| `Microsoft.Win32.Registry` / `RegistryLookup` | HKLM game install lookup | `WindowsGameInstallLocator` (Infrastructure, runtime-guarded); never in shared projects |
| `UserPreferences.settings` (ApplicationSettingsBase) | all settings | `JsonSettingsService` (versioned JSON, atomic writes, legacy migration) |
| `app.config` userSettings | settings defaults | defaults live in `AppSettings` |
| `user32.dll` close-button P/Invoke (`WindowLogic.cs`) | disable close during compile | view-model `IsBusy` gating + `CancellationToken` |
| `RemoveReadOnly.exe` (bundled, admin UAC) | fixing read-only game folders | `IFilePermissionService` (Linux: owner-write bits; Windows: attribute clear + optional helper) |
| `Assembly.GetExecutingAssembly().Location` + write-beside-exe | templates, backups | per-user data dir (`IAppDataLocator`) |
| hardcoded `onyx.exe` / `UseShellExecute` / string-joined args | Onyx CLI invocation | `IExternalProcessService` (ArgumentList), platform-appropriate exe name, full path stored in settings |
| `MediaFoundationResampler` (PS2 audio) | MSV resample | `WdlResamplingSampleProvider` (managed, cross-platform) |
| hardcoded `GH3.exe` check as the *only* validity test | game path validation | data-folder validation (`GameInstallValidator`), exe optional on Linux |

## 3. Form -> view/view-model mapping

| WinForms form | Avalonia view | View model |
|---|---|---|
| `MasterForm` (launcher + console) | `Views/MainWindow.axaml` | `MainWindowViewModel` |
| `CompileSong` (song compiler) | `Views/CompileSongWindow.axaml` | `CompileSongViewModel` |
| `ImportSGH` | `Views/ImportSghWindow.axaml` | `ImportSghViewModel` |
| `PakTools` | `Views/PakToolsWindow.axaml` | `PakToolsViewModel` |
| `WadTools` | `Views/WadToolsWindow.axaml` | `WadToolsViewModel` |
| `SongListManager` | `Views/SongListManagerWindow.axaml` | `SongListManagerViewModel` |
| `ProgramSettings` | `Views/SettingsWindow.axaml` | `SettingsViewModel` |
| MessageBox / `Exceptions.HandleException` | `Views/MessageDialog.axaml` | `AvaloniaNotificationService` |

Behavior notes:

* Game/platform radio groups, tab switching, platform enablement, checksum
  display, "Compile to Folder"/"Export to SGH"/"Disabled" button text, cover /
  co-op / rendered-preview toggles, preview-time syncing (12 fields + set-end
  semantics), backing-track lists, WTDE and GH5+ settings tabs are all ported
  1:1 into `CompileSongViewModel`.
* `.ghproj` files remain byte-compatible (same field names, same JSON
  `DefaultValueHandling` behavior - see `SongProjectData`).
* The launcher console (toolkit `Console.WriteLine` output) is captured by
  `ConsoleLogSink` into the main window and a per-user log file.
* Command-line `.ghproj`/`.sgh` opening is preserved (`Program.Main` -> `App` ->
  `MainWindowViewModel.OpenInputFile`).

## 4. Known compatibility risks

1. **Audio**: MP3/OGG/FLAC/WAV decoding + FSB encoding is done by GH-Toolkit
   through **ffmpeg/ffprobe** (required on PATH, or configured in Settings).
   Without ffmpeg, audio compilation fails with a clear error. On Windows the
   old app had the same dependency via the toolkit.
2. **PS2 resampler**: the Windows-only `MediaFoundationResampler` was replaced
   with NAudio's managed `WdlResamplingSampleProvider`. Output is 16-bit stereo
   at the same sample rate; sample values can differ slightly from Media
   Foundation's resampler.
3. **PAK/WAD byte parity**: entry names are normalized to backslashes before
   checksumming (unchanged), and on-disk paths now use forward slashes on
   Linux. Output PAK/WAD files checksum-identically to Windows; raw flag-path
   strings are normalized to backslashes to stay byte-identical.
4. **PS3/360 packages** require Onyx; `CompileWithOnyx` now launches with
   `ArgumentList` and accepts a full executable path.
5. **Registry-based game discovery** is best-effort (several known key
   locations); manual selection always works and is persisted.
6. **`songYear`/genre `[DefaultValue]` quirks** from the legacy serialization
   are preserved deliberately (fields without `[DefaultValue]` reset to 0 when
   a project omits them).
7. **linux-arm64**: publishes (pure managed + Skia/HarfBuzz arm64 natives);
   `ffmpeg`/`onyx` must be available for the target architecture.
8. **Read-only app mounts (AppImage)**: the toolkit no longer writes beside the
   executable. User checksum keys (`keys_user.txt`/`keys_qs_user.txt`) go to the
   app folder when writable, otherwise to the per-user data folder
   (`$XDG_DATA_HOME/Honeycomb/QBDebug` on Linux, `%LOCALAPPDATA%\Honeycomb\QBDebug`
   on Windows); writer failures degrade gracefully instead of failing compilation.
   Temporary QS extraction in PAK compilation uses the system temp directory.
   Bundled `QBDebug/keys*.txt` and `PS2Pak.dbg` remain read-only resources read
   from the app folder.
9. **Hashed PAK entry names (fresh-install songlist lookup)**: the bundled
   BetterGH3 `customs.pak.xen` stores its songlist under a hashed entry name
   (`0x2cbadf14...` for `dlc_songlist.qb`) that is not present in the bundled
   `keys.txt`. `GetSongListPak`/`GetDownloadPak` now fall back to matching the
   leading checksum portion of hashed `FullName`s, so the first compile works on a
   fresh install. Windows masked this bug by accumulating the checksum in
   `keys_user.txt` over time; AppImage users hit it immediately.

## 5. Final validation checklist

- [x] `dotnet restore` from a fresh checkout (submodule init + `scripts/init-deps.sh`)
- [x] `dotnet build -c Release` on Linux
- [x] `dotnet test -c Release` (unit, integration, Avalonia headless smoke)
- [x] `dotnet publish -c Release -r linux-x64`
- [x] `dotnet publish -c Release -r win-x64`
- [x] `dotnet publish -c Release -r linux-arm64`
- [x] App starts headless; window/VM creation smoke-tested
- [x] Settings persist across restarts (atomic JSON, malformed recovery, legacy migration)
- [x] No `System.Windows.Forms` / `Microsoft.Win32.Registry` in shared projects
- [x] Resource casing (`Skeletons.txt`, `SongCategories.txt`) verified on case-sensitive FS
- [x] UI never blocks on async work: no `.Wait()`/`GetAwaiter().GetResult()` in app projects
      (enforced by a source-scan regression test); SGH load/extract/conversion runs off the
      UI thread with cancellation + busy-state guarding; file pickers are timeout-guarded
- [x] Wine/Proton prefix discovery + data-folder validation tested
- [x] CLI parity: compile/extract/import flows tested against the same GH-Toolkit algorithms
