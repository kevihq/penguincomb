using System.Collections.ObjectModel;
using System.Text;
using Avalonia.Threading;

namespace PenguinComb.App.Services;

/// <summary>
/// Redirects Console output (used heavily by GH-Toolkit) into an in-memory buffer that
/// the main window displays, and appends everything to a per-user log file.
/// Line delivery to the UI is batched so heavy log bursts never flood the dispatcher.
/// </summary>
public class ConsoleLogSink : TextWriter
{
    private readonly object _lock = new();
    private readonly string _logPath;
    private StreamWriter? _fileWriter;
    private readonly List<string> _pending = new();
    private bool _flushQueued;

    public ObservableCollection<string> Lines { get; } = new();
    public const int MaxLines = 5000;

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

            // Coalesce output into a single dispatcher pass so bursts of console
            // writes (large PAK/QB compiles) do not queue one post per line.
            _pending.Add(timestamped);
            if (_flushQueued)
            {
                return;
            }
            _flushQueued = true;
            Dispatcher.UIThread.Post(FlushPending, DispatcherPriority.Background);
        }
    }

    private void FlushPending()
    {
        List<string> batch;
        lock (_lock)
        {
            batch = [.. _pending];
            _pending.Clear();
            _flushQueued = false;
        }

        if (batch.Count == 0)
        {
            return;
        }

        foreach (string text in batch)
        {
            foreach (string line in text.Split('\n'))
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
        }
    }
}
