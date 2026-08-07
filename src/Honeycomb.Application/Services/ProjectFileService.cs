using Honeycomb.Application.Abstractions;
using Honeycomb.Application.Models;
using GH_Toolkit_Core.INI;
using IniParser.Model;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace Honeycomb.Application.Services;

/// <summary>
/// Saves/loads <c>.ghproj</c> song projects (JSON, compatible with the legacy format),
/// manages the default template, and imports Clone Hero (CH) song folders.
/// Paths inside a project file are stored relative to the project when possible.
/// </summary>
public class ProjectFileService
{
    private readonly IAppDataLocator _appData;
    private readonly IFileDialogService _dialogs;

    private const string GhprojFileFilter = "GHProj files (*.ghproj)|*.ghproj|All files (*.*)|*.*";

    public ProjectFileService(IAppDataLocator appData, IFileDialogService dialogs)
    {
        _appData = appData;
        _dialogs = dialogs;
    }

    public string DefaultTemplateFolder => _appData.TemplatesDirectory;
    public string DefaultTemplatePath => Path.Combine(DefaultTemplateFolder, "default.ghproj");

    /// <summary>Creates the default template when missing.</summary>
    public void EnsureDefaultTemplate(SongProjectData data)
    {
        if (File.Exists(DefaultTemplatePath))
        {
            return;
        }

        Directory.CreateDirectory(DefaultTemplateFolder);
        var clean = new SongProjectData();
        clean.projectPath = "";
        string json = JsonConvert.SerializeObject(clean, Formatting.Indented,
            new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore });
        File.WriteAllText(DefaultTemplatePath, json);
    }

    public string GetRelativePath(string filePath, string projectFilePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return string.Empty;
        }
        if (string.IsNullOrEmpty(projectFilePath))
        {
            return filePath;
        }

        string projectDir = Path.GetDirectoryName(projectFilePath)!;

        if (!Path.IsPathRooted(filePath))
        {
            filePath = Path.Combine(projectDir, filePath);
        }

        filePath = Path.GetFullPath(filePath);

        Uri fileUri = new Uri(filePath);
        Uri projectUri = new Uri(projectDir + Path.DirectorySeparatorChar);

        Uri relativeUri = projectUri.MakeRelativeUri(fileUri);
        string relativePath = Uri.UnescapeDataString(relativeUri.ToString().Replace('/', Path.DirectorySeparatorChar));

        string[] relativeParts = relativePath.Split(Path.DirectorySeparatorChar);
        int upDirectoryCount = relativeParts.Count(part => part == "..");

        return upDirectoryCount <= 1 ? relativePath : filePath;
    }

    public string GetAbsolutePath(string filePath, string projectFilePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return string.Empty;
        }
        if (string.IsNullOrEmpty(projectFilePath))
        {
            return filePath;
        }

        string projectDir = Path.GetDirectoryName(projectFilePath)!;
        if (!Path.IsPathRooted(filePath))
        {
            filePath = Path.Combine(projectDir, filePath);
        }

        return Path.GetFullPath(filePath);
    }

    private static IEnumerable<string> SplitList(string? joined) =>
        string.IsNullOrEmpty(joined) ? [] : joined.Split(';', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Converts every path field to absolute (used right before compiling).</summary>
    public void SetAllToAbsolute(SongProjectData data)
    {
        if (string.IsNullOrEmpty(data.projectPath))
        {
            return;
        }

        string projectPath = data.projectPath;
        data.kickPath = GetAbsolutePath(data.kickPath, projectPath);
        data.snarePath = GetAbsolutePath(data.snarePath, projectPath);
        data.cymbalsPath = GetAbsolutePath(data.cymbalsPath, projectPath);
        data.tomsPath = GetAbsolutePath(data.tomsPath, projectPath);
        data.guitarPath = GetAbsolutePath(data.guitarPath, projectPath);
        data.bassPath = GetAbsolutePath(data.bassPath, projectPath);
        data.vocalsPath = GetAbsolutePath(data.vocalsPath, projectPath);
        data.backingPaths = string.Join(";", SplitList(data.backingPaths).Select(p => GetAbsolutePath(p, projectPath)));
        data.crowdPath = GetAbsolutePath(data.crowdPath, projectPath);
        data.previewAudioPath = GetAbsolutePath(data.previewAudioPath, projectPath);

        data.midiPath = GetAbsolutePath(data.midiPath, projectPath);
        data.perfPath = GetAbsolutePath(data.perfPath, projectPath);
        data.skaPath = GetAbsolutePath(data.skaPath, projectPath);
        data.songScriptPath = GetAbsolutePath(data.songScriptPath, projectPath);
        data.lipsyncPath = GetAbsolutePath(data.lipsyncPath, projectPath);

        data.guitarPathGh3 = GetAbsolutePath(data.guitarPathGh3, projectPath);
        data.rhythmPathGh3 = GetAbsolutePath(data.rhythmPathGh3, projectPath);
        data.backingPathsGh3 = string.Join(";", SplitList(data.backingPathsGh3).Select(p => GetAbsolutePath(p, projectPath)));
        data.coopGuitarPath = GetAbsolutePath(data.coopGuitarPath, projectPath);
        data.coopRhythmPath = GetAbsolutePath(data.coopRhythmPath, projectPath);
        data.coopBackingPaths = string.Join(";", SplitList(data.coopBackingPaths).Select(p => GetAbsolutePath(p, projectPath)));
        data.crowdPathGh3 = GetAbsolutePath(data.crowdPathGh3, projectPath);
        data.previewAudioPathGh3 = GetAbsolutePath(data.previewAudioPathGh3, projectPath);
        data.midiPathGh3 = GetAbsolutePath(data.midiPathGh3, projectPath);
        data.perfPathGh3 = GetAbsolutePath(data.perfPathGh3, projectPath);
        data.skaPathGh3 = GetAbsolutePath(data.skaPathGh3, projectPath);
        data.songScriptPathGh3 = GetAbsolutePath(data.songScriptPathGh3, projectPath);
    }

    /// <summary>Converts every path field back to project-relative (used after compiling).</summary>
    public void SetAllToRelative(SongProjectData data)
    {
        if (string.IsNullOrEmpty(data.projectPath))
        {
            return;
        }

        string projectPath = data.projectPath;
        data.kickPath = GetRelativePath(data.kickPath, projectPath);
        data.snarePath = GetRelativePath(data.snarePath, projectPath);
        data.cymbalsPath = GetRelativePath(data.cymbalsPath, projectPath);
        data.tomsPath = GetRelativePath(data.tomsPath, projectPath);
        data.guitarPath = GetRelativePath(data.guitarPath, projectPath);
        data.bassPath = GetRelativePath(data.bassPath, projectPath);
        data.vocalsPath = GetRelativePath(data.vocalsPath, projectPath);
        data.backingPaths = string.Join(";", SplitList(data.backingPaths).Select(p => GetRelativePath(p, projectPath)));
        data.crowdPath = GetRelativePath(data.crowdPath, projectPath);
        data.previewAudioPath = GetRelativePath(data.previewAudioPath, projectPath);

        data.midiPath = GetRelativePath(data.midiPath, projectPath);
        data.perfPath = GetRelativePath(data.perfPath, projectPath);
        data.skaPath = GetRelativePath(data.skaPath, projectPath);
        data.songScriptPath = GetRelativePath(data.songScriptPath, projectPath);
        data.lipsyncPath = GetRelativePath(data.lipsyncPath, projectPath);

        data.guitarPathGh3 = GetRelativePath(data.guitarPathGh3, projectPath);
        data.rhythmPathGh3 = GetRelativePath(data.rhythmPathGh3, projectPath);
        data.backingPathsGh3 = string.Join(";", SplitList(data.backingPathsGh3).Select(p => GetRelativePath(p, projectPath)));
        data.coopGuitarPath = GetRelativePath(data.coopGuitarPath, projectPath);
        data.coopRhythmPath = GetRelativePath(data.coopRhythmPath, projectPath);
        data.coopBackingPaths = string.Join(";", SplitList(data.coopBackingPaths).Select(p => GetRelativePath(p, projectPath)));
        data.crowdPathGh3 = GetRelativePath(data.crowdPathGh3, projectPath);
        data.previewAudioPathGh3 = GetRelativePath(data.previewAudioPathGh3, projectPath);
        data.midiPathGh3 = GetRelativePath(data.midiPathGh3, projectPath);
        data.perfPathGh3 = GetRelativePath(data.perfPathGh3, projectPath);
        data.skaPathGh3 = GetRelativePath(data.skaPathGh3, projectPath);
        data.songScriptPathGh3 = GetRelativePath(data.songScriptPathGh3, projectPath);
    }

    /// <summary>Saves the project to its current path, or prompts for one when unsaved.</summary>
    public async Task<bool> SaveProjectAsync(SongProjectData data, CancellationToken cancellationToken = default)
    {
        if (File.Exists(data.projectPath))
        {
            string json = data.ToJson();
            await File.WriteAllTextAsync(data.projectPath, json, cancellationToken);
            return true;
        }
        return await SaveProjectAsAsync(data, cancellationToken);
    }

    public async Task<bool> SaveProjectAsAsync(SongProjectData data, CancellationToken cancellationToken = default)
    {
        string? path = await _dialogs.PickSaveFileAsync(new FileDialogOptions
        {
            Title = "Save Project As",
            Filters = [new FileFilter("GHProj files", "*.ghproj"), new FileFilter("All files", "*.*")],
            SuggestedFileName = $"{data.songName}.ghproj"
        }, cancellationToken);

        if (path is null)
        {
            return false;
        }

        data.projectPath = path;
        string json = data.ToJson();
        await File.WriteAllTextAsync(path, json, cancellationToken);
        return true;
    }

    public async Task<SongProjectData?> LoadProjectAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        string json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var data = SongProjectData.FromJson(json);
        if (data is null)
        {
            return null;
        }

        if (Path.GetFileName(Path.GetDirectoryName(filePath)) != "Templates")
        {
            data.ghprojFromLoad = filePath;
        }
        data.projectPath = data.ghprojFromLoad;
        return data;
    }

    /// <summary>Imports a Clone Hero song folder into the project data.</summary>
    public void LoadFromChFolder(SongProjectData data, string folderPath)
    {
        string midiRegexCh = @".*\.mid$";
        string chartRegexCh = @".*\.chart$";

        foreach (string file in Directory.GetFiles(folderPath))
        {
            string ext = Path.GetExtension(file).ToLower();
            if (ext == ".ini")
            {
                var ini = iniParser.ReadIniFromPath(file);
                string? songData = null;

                foreach (var section in ini.Sections)
                {
                    if (string.Equals(section.SectionName, "song", StringComparison.OrdinalIgnoreCase))
                    {
                        songData = section.SectionName;
                        break;
                    }
                }

                if (songData != null)
                {
                    ApplySongIni(data, ini, songData);
                }
            }
            else if (Regex.IsMatch(file, midiRegexCh))
            {
                data.midiPathGh3 = file;
                data.midiPath = file;
            }
            else if (Regex.IsMatch(file, chartRegexCh) && string.IsNullOrEmpty(data.midiPathGh3))
            {
                data.midiPathGh3 = file;
                data.midiPath = file;
            }
        }

        var assignment = iniParser.AssignFiles(folderPath, "GH3");
        data.guitarPathGh3 = assignment.Guitar ?? data.guitarPathGh3;
        data.rhythmPathGh3 = assignment.Rhythm ?? data.rhythmPathGh3;
        data.crowdPathGh3 = assignment.Crowd ?? data.crowdPathGh3;
        data.previewAudioPathGh3 = assignment.Preview ?? data.previewAudioPathGh3;
        data.backingPathsGh3 = string.Join(";", assignment.BackingTracks);
        data.useRenderedPreviewGh3 = assignment.RenderedPreview;

        assignment = iniParser.AssignFiles(folderPath, "GHWT");
        data.kickPath = assignment.KickDrum ?? data.kickPath;
        data.snarePath = assignment.SnareDrum ?? data.snarePath;
        data.tomsPath = assignment.Toms ?? data.tomsPath;
        data.cymbalsPath = assignment.Cymbals ?? data.cymbalsPath;
        data.guitarPath = assignment.Guitar ?? data.guitarPath;
        data.bassPath = assignment.Bass ?? data.bassPath;
        data.vocalsPath = assignment.Vocals ?? data.vocalsPath;
        data.crowdPath = assignment.Crowd ?? data.crowdPath;
        data.previewAudioPath = assignment.Preview ?? data.previewAudioPath;
        data.backingPaths = string.Join(";", assignment.BackingTracks);
        data.useRenderedPreview = assignment.RenderedPreview;
    }

    private void ApplySongIni(SongProjectData data, IniData ini, string iniSection)
    {
        var songData = iniParser.ParseSongIni(ini, iniSection);

        data.title = songData.Title ?? string.Empty;
        data.artist = songData.Artist ?? string.Empty;
        data.chartAuthor = songData.Charter ?? string.Empty;
        data.songName = songData.Checksum ?? string.Empty;
        data.album = songData.Album ?? string.Empty;

        if (songData.Year.HasValue)
        {
            data.songYear = songData.Year.Value;
        }

        if (songData.BandTier.HasValue) data.bandTier = songData.BandTier.Value;
        if (songData.GuitarTier.HasValue) data.guitarTier = songData.GuitarTier.Value;
        if (songData.BassTier.HasValue) data.bassTier = songData.BassTier.Value;
        if (songData.DrumsTier.HasValue) data.drumsTier = songData.DrumsTier.Value;
        if (songData.VocalsTier.HasValue) data.vocalsTier = songData.VocalsTier.Value;
        if (songData.SustainCutoffThreshold.HasValue) data.sustainThreshold = songData.SustainCutoffThreshold.Value;
        if (songData.HopoFrequency.HasValue) data.hmxHopoVal = songData.HopoFrequency.Value;

        data.previewStart = songData.PreviewStartTime;
        data.previewEnd = songData.PreviewEndTime == 0 ? songData.PreviewStartTime + 30000 : songData.PreviewEndTime;
    }
}
