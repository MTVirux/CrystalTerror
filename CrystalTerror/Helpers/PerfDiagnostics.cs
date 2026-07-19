namespace CrystalTerror.Helpers;

using System.Diagnostics;
using System.Text;

/// <summary>
/// DEBUG-only in-memory collector for import-timing samples, surfaced by the Debug config tab.
/// Every recording entry point is marked [Conditional("DEBUG")], so in Release builds the
/// call sites are compiled out entirely - no timing is collected and there is no overhead.
/// </summary>
public static class PerfDiagnostics
{
    public sealed class PerfSample
    {
        public DateTime TimeUtc;
        public string Source = string.Empty;
        public double ImportMs;
        public double MergeMs;
        public double SaveMs;
        public double TotalMs;
        public int RetainerCount;
        public double RetainerTotalMs;
        public double RetainerIpcMs;
    }

    private const int MaxSamples = 25;
    private static readonly object _lock = new();
    private static readonly List<PerfSample> _samples = new();

    // Retainer sub-timing handed up from PopulateRetainers to the top-level recorder.
    // ThreadStatic so a future background import can't clobber a main-thread one.
    [ThreadStatic]
    private static (int Count, double TotalMs, double IpcMs)? _pendingRetainer;

    /// <summary>Clears the pending retainer sub-timing at the start of an import.</summary>
    [Conditional("DEBUG")]
    public static void BeginImport() => _pendingRetainer = null;

    /// <summary>Records retainer-population sub-timing for the in-flight import.</summary>
    [Conditional("DEBUG")]
    public static void SetRetainerTiming(int count, double totalMs, double ipcMs)
        => _pendingRetainer = (count, totalMs, ipcMs);

    /// <summary>Records a completed import sample, folding in any pending retainer sub-timing.</summary>
    [Conditional("DEBUG")]
    public static void RecordImport(string source, double importMs, double mergeMs, double saveMs)
    {
        var rt = _pendingRetainer;
        _pendingRetainer = null;

        var sample = new PerfSample
        {
            TimeUtc = DateTime.UtcNow,
            Source = source,
            ImportMs = importMs,
            MergeMs = mergeMs,
            SaveMs = saveMs,
            TotalMs = importMs + mergeMs + saveMs,
            RetainerCount = rt?.Count ?? 0,
            RetainerTotalMs = rt?.TotalMs ?? 0,
            RetainerIpcMs = rt?.IpcMs ?? 0,
        };

        lock (_lock)
        {
            _samples.Add(sample);
            if (_samples.Count > MaxSamples)
                _samples.RemoveAt(0);
        }
    }

    /// <summary>Returns a copy of the recorded samples in insertion order (oldest first).</summary>
    public static List<PerfSample> Snapshot()
    {
        lock (_lock)
            return new List<PerfSample>(_samples);
    }

    public static void Clear()
    {
        lock (_lock)
            _samples.Clear();
    }

    /// <summary>Builds an aligned plain-text report of all samples (newest first) for the clipboard.</summary>
    public static string BuildReport()
    {
        var samples = Snapshot();
        samples.Reverse(); // newest first

        var ver = typeof(CrystalTerrorPlugin).Assembly.GetName().Version?.ToString() ?? "?";
        var sb = new StringBuilder();
        sb.AppendLine($"CrystalTerror import-timing report (v{ver})");
        sb.AppendLine($"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z - {samples.Count} sample(s) - all times in ms");
        sb.AppendLine();
        sb.AppendLine(Row("Time(UTC)", "Source", "Import", "Merge", "Save", "Total", "Ret", "RetIPC", "RetTot"));
        sb.AppendLine(new string('-', 96));
        foreach (var s in samples)
        {
            sb.AppendLine(Row(
                s.TimeUtc.ToString("HH:mm:ss"),
                s.Source,
                s.ImportMs.ToString("F1"),
                s.MergeMs.ToString("F1"),
                s.SaveMs.ToString("F1"),
                s.TotalMs.ToString("F1"),
                s.RetainerCount.ToString(),
                s.RetainerIpcMs.ToString("F1"),
                s.RetainerTotalMs.ToString("F1")));
        }
        return sb.ToString();
    }

    private static string Row(string time, string src, string import, string merge, string save, string total, string ret, string retIpc, string retTot)
        => $"{time,-9}  {src,-16}  {import,8}  {merge,7}  {save,7}  {total,8}  {ret,4}  {retIpc,8}  {retTot,8}";
}
