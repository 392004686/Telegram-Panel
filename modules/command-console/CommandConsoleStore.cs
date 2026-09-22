using System.Text.Json;

namespace TelegramPanel.CommandConsole;
public sealed class CommandConsoleStore
{
    private readonly string _root; private readonly object _gate = new();
    public CommandConsoleStore(string root) { _root = root; Directory.CreateDirectory(Path.Combine(root, "runs")); }
    public async Task AppendAsync(RunEvent value, CancellationToken ct)
    { var dir = Path.Combine(_root, "runs", value.TimeUtc.ToString("yyyy-MM")); Directory.CreateDirectory(dir); var path = Path.Combine(dir, value.RunId + ".jsonl"); var json = JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web)); lock (_gate) File.AppendAllText(path, json + Environment.NewLine); await Task.CompletedTask; }
    public IReadOnlyList<string> List() => Directory.EnumerateFiles(Path.Combine(_root, "runs"), "*.jsonl", SearchOption.AllDirectories).OrderByDescending(x => x).Select(Path.GetFileNameWithoutExtension).ToArray()!;
    public IReadOnlyList<RunEvent>? Read(string runId) { var file = Directory.EnumerateFiles(Path.Combine(_root, "runs"), runId + ".jsonl", SearchOption.AllDirectories).SingleOrDefault(); return file is null ? null : File.ReadLines(file).Select(x => JsonSerializer.Deserialize<RunEvent>(x)!).ToArray(); }
}
public sealed record RunEvent(string Type, string RunId, DateTimeOffset TimeUtc, string? Message, object? Data, object? Error);
public sealed record RunResult(string RunId, string RawCommand, bool Success, IReadOnlyList<RunEvent> Events);
