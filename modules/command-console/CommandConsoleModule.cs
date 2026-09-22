using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using TelegramPanel.Core.Interfaces;
using TelegramPanel.Core.Services.Telegram;
using TelegramPanel.Modules;
using TelegramPanel.Web.Services;

namespace TelegramPanel.CommandConsole;

public sealed class CommandConsoleModule : ITelegramPanelModule, IModuleUiProvider
{
    public ModuleManifest Manifest => new() { Id = "command-console", Name = "命令控制台", Version = "1.0.1", Host = new HostCompatibility { Min = "1.31.76" }, Entry = new ModuleEntryPoint { Assembly = "TelegramPanel.CommandConsole.dll", Type = GetType().FullName! } };
    public void ConfigureServices(IServiceCollection services, ModuleHostContext context)
    {
        services.AddSingleton(new CommandConsoleStore(Path.Combine(context.ModulesRootPath, "data", "command-console")));
    }
    public void MapEndpoints(IEndpointRouteBuilder endpoints, ModuleHostContext context)
    {
        endpoints.MapGet("/ext/command-console/console", () => Results.Content(ConsolePage, "text/html; charset=utf-8")).RequireAuthorization();
        var group = endpoints.MapGroup("/api/panel/extensions/command-console").RequireAuthorization();
        group.MapPost("/runs", async (RunRequest request, CommandConsoleStore store, IServiceProvider sp, CancellationToken ct) =>
        {
            var run = await CommandRunner.RunAsync(request.Command, store, sp, ct);
            return Results.Ok(run);
        });
        group.MapGet("/runs", (CommandConsoleStore store) => Results.Ok(store.List()));
        group.MapGet("/runs/{runId}", (string runId, CommandConsoleStore store) => store.Read(runId) is { } events ? Results.Ok(events) : Results.NotFound());
    }
    public IEnumerable<ModuleNavItem> GetNavItems(ModuleHostContext context)
    {
        yield return new ModuleNavItem { Title = "命令控制台", Href = "/ext/command-console/console", Icon = "terminal", Group = "工具", Order = 10 };
    }
    public IEnumerable<ModulePageDefinition> GetPages(ModuleHostContext context) => Array.Empty<ModulePageDefinition>();

    private const string ConsolePage = """
<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>命令控制台</title>
<style>body{font-family:system-ui,-apple-system,"Segoe UI",sans-serif;background:#f4f7fb;color:#172033;margin:0;padding:28px}main{max-width:1100px;margin:auto}textarea{width:100%;min-height:110px;padding:12px;border:1px solid #d7dfeb;border-radius:8px;font:14px monospace;box-sizing:border-box}button{background:#1677ff;color:#fff;border:0;border-radius:7px;padding:9px 18px;cursor:pointer;margin-top:10px}.card{background:#fff;padding:20px;border-radius:12px;box-shadow:0 2px 12px #dfe6f0;margin-bottom:18px}pre{white-space:pre-wrap;max-height:480px;overflow:auto;background:#101827;color:#d7e5ff;padding:14px;border-radius:8px}.muted{color:#667085}.history button{background:#eef4ff;color:#1459b8;margin:4px;padding:6px 10px}</style></head>
<body><main><h1>命令控制台</h1><p class="muted">执行宿主 Telegram 操作并查看原始步骤日志。每行一条命令，支持固定白名单命令。</p>
<section class="card"><textarea id="command" placeholder="例如：group.list account=2 refresh=true"></textarea><br><button id="run">执行命令</button></section>
<section class="card"><h2>运行结果</h2><pre id="output">等待执行…</pre></section>
<section class="card history"><h2>历史运行</h2><div id="history">加载中…</div></section></main>
<script>
const base='/api/panel/extensions/command-console'; const out=document.getElementById('output');
async function load(){const r=await fetch(base+'/runs');const ids=await r.json();document.getElementById('history').innerHTML=ids.length?ids.map(id=>'<button data-id="'+id+'">'+id+'</button>').join(''):'暂无记录';document.querySelectorAll('[data-id]').forEach(b=>b.onclick=async()=>{out.textContent=JSON.stringify(await (await fetch(base+'/runs/'+b.dataset.id)).json(),null,2)})}
document.getElementById('run').onclick=async()=>{const command=document.getElementById('command').value.trim();if(!command)return;out.textContent='执行中…';const r=await fetch(base+'/runs',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({command})});out.textContent=JSON.stringify(await r.json(),null,2);load()};load();
</script></body></html>
""";
    public sealed record RunRequest(string Command);
}

internal static class CommandRunner
{
    public static async Task<RunResult> RunAsync(string raw, CommandConsoleStore store, IServiceProvider services, CancellationToken ct)
    {
        var runId = Guid.NewGuid().ToString("N");
        var started = DateTimeOffset.UtcNow;
        var events = new List<RunEvent>();
        void Add(string type, object? data = null, string? message = null) => events.Add(new(type, runId, DateTimeOffset.UtcNow, message, data, null));
        try
        {
            var command = CommandParser.Parse(raw);
            Add("run.started", new { moduleVersion = "1.0.0" });
            Add("command.parsed", command);
            var account = command.RequireInt("account");
            Add("step.started", new { step = command.Action });
            object result = command.Action switch
            {
                "account.sync" => await services.GetRequiredService<DataSyncService>().SyncAccountAsync(account, ct),
                "chat.join" => await services.GetRequiredService<AccountTelegramToolsService>().JoinChatOrChannelAsync(account, command.Require("target"), ct),
                "group.list" => await services.GetRequiredService<IGroupService>().GetVisibleGroupsAsync(account, ct),
                "group.invite" => await InviteAsync(command, services, account, ct),
                _ => throw new CommandException($"不支持的命令：{command.Action}")
            };
            Add("step.succeeded", result);
            Add("run.succeeded", new { elapsedMs = (DateTimeOffset.UtcNow - started).TotalMilliseconds });
        }
        catch (Exception ex)
        {
            Add("step.failed", null, ex.Message);
            Add("run.failed", null, ex.Message);
        }
        foreach (var e in events) await store.AppendAsync(e, ct);
        return new RunResult(runId, raw, events.LastOrDefault()?.Type == "run.succeeded", events);
    }
    private static async Task<object> InviteAsync(ParsedCommand command, IServiceProvider services, int account, CancellationToken ct)
    {
        var group = long.Parse(command.Require("group"));
        var names = command.Require("usernames").Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var service = services.GetRequiredService<IGroupService>(); var results = new List<object>();
        foreach (var name in names) { ct.ThrowIfCancellationRequested(); var sw = System.Diagnostics.Stopwatch.StartNew(); var r = await service.InviteUserAsync(account, group, name); results.Add(new { target = name, r.Success, r.Error, r.UserId, elapsedMs = sw.ElapsedMilliseconds }); if (command.TryGetInt("delay", out var delay) && delay > 0) await Task.Delay(delay, ct); }
        return results;
    }
}

internal sealed class CommandException(string message) : Exception(message);

internal sealed record ParsedCommand(string Action, IReadOnlyDictionary<string, string> Args)
{
    public string Require(string key) => Args.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : throw new CommandException($"缺少参数：{key}");
    public int RequireInt(string key) => int.Parse(Require(key));
    public bool TryGetInt(string key, out int value) => int.TryParse(Args.GetValueOrDefault(key), out value);
}

internal static class CommandParser
{
    public static ParsedCommand Parse(string input)
    {
        var tokens = System.Text.RegularExpressions.Regex.Matches(input ?? "", "(?:[^\\s\\\"']+|\\\"[^\\\"]*\\\"|'[^']*')+").Select(m => m.Value.Trim('"', '\'')).ToArray();
        if (tokens.Length == 0 || !tokens[0].Contains('.')) throw new CommandException("命令必须使用 action.subaction 格式");
        var args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in tokens.Skip(1)) { var i = token.IndexOf('='); if (i <= 0) throw new CommandException($"参数格式错误：{token}"); args[token[..i]] = token[(i + 1)..].Trim('"', '\''); }
        return new ParsedCommand(tokens[0].ToLowerInvariant(), args);
    }
}
