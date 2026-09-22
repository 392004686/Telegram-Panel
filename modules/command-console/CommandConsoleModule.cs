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
    public ModuleManifest Manifest => new() { Id = "command-console", Name = "命令控制台", Version = "1.0.4", Host = new HostCompatibility { Min = "1.31.76" }, Entry = new ModuleEntryPoint { Assembly = "TelegramPanel.CommandConsole.dll", Type = GetType().FullName! } };
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
<style>body{font-family:system-ui,-apple-system,"Segoe UI",sans-serif;background:#f3f6fb;color:#172033;margin:0;padding:28px}main{max-width:1120px;margin:auto}textarea{width:100%;min-height:96px;padding:12px;border:1px solid #d7dfeb;border-radius:8px;font:14px monospace;box-sizing:border-box}button,select{background:#fff;color:#245ea8;border:1px solid #cbd9ea;border-radius:7px;padding:8px 14px;cursor:pointer;margin:4px}button.primary{background:#1677ff;color:#fff;border-color:#1677ff}.card{background:#fff;padding:20px;border-radius:14px;box-shadow:0 2px 12px #dfe6f0;margin-bottom:18px}.muted{color:#667085}.history button{background:#f4f8ff;color:#1459b8;margin:4px;padding:9px 12px;text-align:left}.event{background:#fff;border:1px solid #e3eaf3;border-left:4px solid #8eabcf;padding:11px 14px;margin:9px 0;border-radius:8px}.event.ok{border-left-color:#22a06b}.event.fail{border-left-color:#d14343}.result{background:#fbfdff;border:1px solid #e0e7f0;border-radius:9px;padding:14px;margin:10px 0}.kv{display:grid;grid-template-columns:150px 1fr;gap:8px}.raw{margin-top:14px;border-top:1px solid #e4eaf2;padding-top:10px}.raw pre{white-space:pre-wrap;max-height:420px;overflow:auto;background:#172033;color:#e8f0ff;padding:14px;border-radius:8px}</style></head>
<body><main><h1>命令控制台</h1><p class="muted">执行宿主 Telegram 操作并查看原始步骤日志。每行一条命令，支持固定白名单命令。</p>
<section class="card"><textarea id="command" placeholder="例如：group.list account=2 refresh=true"></textarea><br><button id="run">执行命令</button></section>
<section class="card"><div style="display:flex;justify-content:space-between;align-items:center"><h2>运行结果</h2><label>展示：<select id="mode"><option value="auto">自动</option><option value="timeline">时间线</option><option value="table">表格</option><option value="card">卡片</option></select></label></div><div id="output" class="result">等待执行…</div></section>
<section class="card history"><h2>历史运行</h2><div id="history">加载中…</div></section></main>
<script>
const base='/api/panel/extensions/command-console'; const out=document.getElementById('output');
const EVENT={'run.started':'命令开始','command.parsed':'命令解析','step.started':'步骤开始','step.succeeded':'步骤成功','step.failed':'步骤失败','run.succeeded':'命令成功','run.failed':'命令失败'};
const ACTION={'group.list':'群组列表','group.get':'群组详情','group.invite':'邀请用户进群','account.sync':'账号同步','chat.join':'加入群组或频道','proxy.list':'代理列表'};
const FIELD={id:'ID',telegramId:'Telegram ID',title:'名称',username:'用户名',memberCount:'成员数',about:'简介',isPublic:'是否公开',link:'链接',proxyId:'代理 ID',proxyMode:'代理模式',proxyType:'代理类型',proxyStatus:'代理状态',moduleVersion:'模块版本',account:'账号',refresh:'刷新'};
const esc=x=>String(x??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c])); const fmt=x=>new Date(x).toLocaleString('zh-CN',{hour12:false});
let mode='auto'; document.getElementById('mode').onchange=e=>{mode=e.target.value;if(window.lastEvents)render(window.lastEvents)};
function value(v,k){if(v===null||v===undefined||v==='')return '';if(k==='link'&&String(v).startsWith('http'))return '<a href="'+esc(v)+'" target="_blank">'+esc(v)+'</a>';if(typeof v==='boolean')return v?'是':'否';if(typeof v==='object')return esc(JSON.stringify(v));return esc(v)}
function record(o){return '<div class="result"><div class="kv">'+Object.entries(o||{}).filter(([k,v])=>v!==null&&v!==undefined&&v!=='').map(([k,v])=>'<b>'+esc(FIELD[k]||k)+'</b><span>'+value(v,k)+'</span>').join('')+'</div></div>'}
function dataView(d){if(Array.isArray(d)){if(!d.length)return '<p>结果（0 条）</p>';if(mode==='card'||(mode==='auto'&&d.length===1))return '<p>结果（'+d.length+' 条）</p>'+d.map((x,i)=>'<div class="result"><b>#'+(i+1)+'</b>'+record(x)+'</div>').join('');const keys=[...new Set(d.flatMap(x=>Object.keys(x||{})))].filter(k=>d.some(x=>x?.[k]!==null&&x?.[k]!==undefined&&x?.[k]!==''));return '<p>结果（'+d.length+' 条）</p><div style="overflow:auto"><table style="width:100%;border-collapse:collapse"><thead><tr>'+keys.map(k=>'<th style="text-align:left;padding:10px;border-bottom:1px solid #dbe4ef">'+esc(FIELD[k]||k)+'</th>').join('')+'</tr></thead><tbody>'+d.map(x=>'<tr>'+keys.map(k=>'<td style="padding:10px;border-bottom:1px solid #eef2f7">'+value(x?.[k],k)+'</td>').join('')+'</tr>').join('')+'</tbody></table></div>'}return record(d)}
function render(events){window.lastEvents=events;const p=events.find(x=>x.type==='command.parsed'),d=events.find(x=>x.type==='run.succeeded'||x.type==='run.failed');let h='<h3>'+esc(ACTION[p?.data?.action]||p?.data?.action||'命令')+'</h3>';h+='<p>运行 ID：'+esc(events[0]?.runId)+'　时间：'+fmt(events[0]?.timeUtc)+'　状态：'+(d?.type==='run.succeeded'?'成功':'失败')+'</p>';h+=events.map(e=>'<div class="event '+(e.type.includes('failed')?'fail':e.type.includes('succeeded')?'ok':'')+'"><b>'+esc(EVENT[e.type]||e.type)+'</b> <small>'+fmt(e.timeUtc)+'</small>'+(e.message?'<div>'+esc(e.message)+'</div>':'')+(e.type==='step.succeeded'&&e.data?dataView(e.data):e.type==='command.parsed'&&e.data?record(e.data):'')+'</div>').join('');h+='<details class="raw"><summary>查看原始 JSON</summary><pre>'+esc(JSON.stringify(events,null,2))+'</pre></details>';out.innerHTML=h}
async function openRun(id){render(await (await fetch(base+'/runs/'+id)).json())}
async function load(){const ids=await (await fetch(base+'/runs')).json();const box=document.getElementById('history');box.innerHTML=ids.length?ids.map(id=>'<button data-id="'+esc(id)+'">'+esc(id)+'</button>').join(''):'暂无记录';box.querySelectorAll('[data-id]').forEach(b=>b.onclick=()=>openRun(b.dataset.id))} 
document.getElementById('run').onclick=async()=>{const command=document.getElementById('command').value.trim();if(!command)return;out.textContent='执行中…';const r=await fetch(base+'/runs',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({command})});const x=await r.json();render(x.events||[]);load()};load();
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
            Add("run.started", new { moduleVersion = "1.0.4" });
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
