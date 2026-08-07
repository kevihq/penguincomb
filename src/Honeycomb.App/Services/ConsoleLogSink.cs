using System.Collections.ObjectModel;
using System.Text;
using Avalonia.Threading;

namespace Honeycomb.App.Services;

/// <summary>
/// Redirects Console output (used heavily by GH-Toolkit) into an in-memory buffer that
/// the main window displays, and appends everything to a per-user log file.
/// </summary>
public class ConsoleLogSink : TextWriter
{
    private readonly object _lock = new();
    private readonly string _logPath;
    private readonly StringBuilder _fileBuffer = new();
    private StreamWriter? _fileWriter;

    public ObservableCollection<string> Lines { get; } = new();
    public const int MaxLines = 2000;

    public override Encoding Encoding => Encoding.UTF8;

    public ConsoleLogSink(string logPath)
    {
        _logPath = logPath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            _fileWriter = new StreamWriter(logPath, append: true) { AutoFlush = true };
        }
        catch
        {
            _fileWriter = null;
        }
    }

    public override void Write(char value) => Write(value.ToString());

    public override void Write(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }
        Append(value);
    }

    public override void WriteLine(string? value)
    {
        Append((value ?? "") + Environment.NewLine);
    }

    private void Append(string text)
    {
        string timestamped = $"[{DateTime.Now:HH:mm:ss}] {text}";
        lock (_lock)
        {
            _fileWriter?.Write(timestamped);
        }

        Dispatcher.UIThread.Post(() =>
        {
            foreach (string line in timestamped.Split('\n'))
            {
                if (line.Length == 0)
                {
                    continue;
                }
                Lines.Add(line.TrimEnd('\r'));
                while (Lines.Count > MaxLines)
                {
                    Lines.RemoveAt(0);
                }
            }
        }, DispatcherPriority.Background);
    }
}
