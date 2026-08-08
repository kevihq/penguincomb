using GH_Toolkit_Core.PAK;
using PenguinComb.Application.Abstractions;
using static GH_Toolkit_Core.PAK.PAK;

namespace PenguinComb.Application.Services;

/// <summary>Console selection for PAK compilation (legacy radio button values).</summary>
public enum PakConsole
{
    Xbox360OrPc,
    Ps3,
    Ps2,
    Wii,
}

/// <summary>PAK extraction/compilation operations (port of the legacy PakTools form).</summary>
public class PakToolService
{
    private readonly IUserNotificationService _notifications;

    public PakToolService(IUserNotificationService notifications)
    {
        _notifications = notifications;
    }

    /// <summary>Extracts every PAK file in the given file-or-folder path.</summary>
    public async Task ExtractAsync(string pakPath, bool convertQ, CancellationToken ct = default)
    {
        var files = PAK.GetFilesFromFolder(pakPath);
        foreach (string file in files)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                Console.WriteLine($"Extracting {file}...");
                await Task.Run(() => PAK.ProcessPAKFromFile(file, !convertQ), ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Extraction failed: {ex.Message}");
            }
        }
    }

    /// <summary>Console name used by the toolkit for a selected console.</summary>
    public static string GetConsoleName(PakConsole console) => console switch
    {
        PakConsole.Ps2 => "PS2",
        PakConsole.Wii => "WII",
        PakConsole.Ps3 => "PS3",
        _ => "360"
    };

    public static string GetConsoleExtension(string console) =>
        GH_Toolkit_Core.Methods.GlobalHelpers.GetConsoleExtension(console);

    /// <summary>Compiles a folder of files into a PAK (and PAB when split).</summary>
    public async Task<(string PakPath, string? PabPath)> CompileAsync(
        string folderPath,
        string game,
        PakConsole console,
        bool splitPab,
        string? assetContext,
        string outputPath,
        CancellationToken ct = default)
    {
        string consoleName = GetConsoleName(console);
        string extension = GetConsoleExtension(consoleName);

        string? context = string.IsNullOrEmpty(assetContext) ? null : assetContext;
        string folderPathUpper = Path.GetDirectoryName(folderPath)?.ToUpper() ?? "";
        bool isQb = folderPathUpper == "QB";
        bool split = (isQb && consoleName != "WII") || splitPab;

        var compiler = new PakCompiler(game, consoleName, context, isQb, split);

        string pakPath = outputPath;
        if (!pakPath.EndsWith(".pak", StringComparison.OrdinalIgnoreCase))
        {
            pakPath += ".pak" + extension;
        }

        Console.WriteLine($"Compiling PAK from {folderPath}...");
        var (pak, pab, qsStrings) = await Task.Run(() => compiler.CompilePAK(folderPath, consoleName, false), ct);

        string pabPath = pakPath.Replace(".pak", ".pab");
        if (pab != null)
        {
            if (qsStrings != null && game != "GH3" && game != "GHA")
            {
                await Task.Run(() => PAK.MakeQsFilesForSplitPak(folderPath, folderPathUpper, consoleName, game, qsStrings, false), ct);
            }
            await File.WriteAllBytesAsync(pakPath, pak, ct);
            await File.WriteAllBytesAsync(pabPath, pab, ct);
            Console.WriteLine("PAK compiled successfully.");
            return (pakPath, pabPath);
        }
        else
        {
            // Non-split: the PAB data was already appended to the PAK by the compiler
            await File.WriteAllBytesAsync(pakPath, pak, ct);
            Console.WriteLine("PAK compiled successfully.");
            return (pakPath, null);
        }
    }
}
