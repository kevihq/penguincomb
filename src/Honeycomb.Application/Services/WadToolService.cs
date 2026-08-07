using GH_Toolkit_Core.PS2;
using Honeycomb.Application.Abstractions;

namespace Honeycomb.Application.Services;

/// <summary>PS2 WAD extraction/compilation (port of the legacy WadTools form).</summary>
public class WadToolService
{
    private readonly IUserNotificationService _notifications;

    public WadToolService(IUserNotificationService notifications)
    {
        _notifications = notifications;
    }

    /// <summary>Files that must exist next to the selected WAD file.</summary>
    public static readonly string[] RequiredFiles = ["DATAP.HED", "DATAP.WAD", "DATAPD.HDP", "DATAPF.HDP"];

    /// <summary>Extracts DATAP.WAD into "&lt;folder&gt;/WAD Extract".</summary>
    public async Task ExtractAsync(string wadFilePath, CancellationToken ct = default)
    {
        string folderPath = Path.GetDirectoryName(wadFilePath) ?? "";
        var missing = RequiredFiles
            .Where(file => !File.Exists(Path.Combine(folderPath, file)))
            .ToList();

        if (missing.Count > 0)
        {
            Console.WriteLine("WAD extraction failed - the following files are missing from the folder:");
            foreach (var file in missing)
            {
                Console.WriteLine($"  {file}");
            }
            return;
        }

        var hedPath = Path.Combine(folderPath, RequiredFiles[0]);
        var wadPath = Path.Combine(folderPath, RequiredFiles[1]);
        string extractPath = Path.Combine(folderPath, "WAD Extract");

        var hedFiles = HED.ReadHEDFile(File.ReadAllBytes(hedPath));
        await Task.Run(() =>
        {
            WAD.ExtractWADFile(hedFiles, File.ReadAllBytes(wadPath), extractPath, false);
        }, ct);
        Console.WriteLine("WAD extracted successfully.");
    }

    /// <summary>Recompiles a WAD from an extracted folder.</summary>
    public async Task CompileAsync(string folderPath, bool recompileQb, CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            WAD.CompileWADFile(folderPath, false, recompileQb);
        }, ct);
        Console.WriteLine("WAD compiled successfully.");
    }
}
