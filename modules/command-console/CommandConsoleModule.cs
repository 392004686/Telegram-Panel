using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TelegramPanel.Core.Interfaces;
using TelegramPanel.Core.Services;
using TelegramPanel.Core.Services.Proxy;
using TelegramPanel.Core.Services.Telegram;
using TelegramPanel.Data;
using TelegramPanel.Modules;
using TelegramPanel.Web.Services;

namespace TelegramPanel.CommandConsole;

public sealed class CommandConsoleModule : ITelegramPanelModule, IModuleUiProvider
{
    public ModuleManifest Manifest => new() { Id = "command-console", Name = "命令控制台", Version = "1.1.0", Host = new HostCompatibility { Min = "1.31.76" }, Entry = new ModuleEntryPoint { Assembly = "TelegramPanel.CommandConsole.dll", Type = GetType().FullName! } };
    public void ConfigureServices(IServiceCollection services, ModuleHostContext context)
    {
        services.AddSingleton(new CommandConsoleStore(Path.Combine(context.ModulesRootPath, "data", "command-console")));
        services.AddSingleton<EngagementCoordinator>();
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
        group.MapDelete("/runs", (CommandConsoleStore store) => { store.Clear(); return Results.Ok(); });
        group.MapDelete("/runs/{runId}", (string runId, CommandConsoleStore store) => store.Delete(runId) ? Results.Ok() : Results.NotFound());
    }
    public IEnumerable<ModuleNavItem> GetNavItems(ModuleHostContext context)
    {
        yield return new ModuleNavItem { Title = "命令控制台", Href = "/ext/command-console/console", Icon = "terminal", Group = "工具", Order = 10 };
    }
    public IEnumerable<ModulePageDefinition> GetPages(ModuleHostContext context) => Array.Empty<ModulePageDefinition>();

    private const string ConsolePage = """
<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>命令控制台</title>
<style>body{font-family:system-ui,-apple-system,"Segoe UI",sans-serif;background:#f3f6fb;color:#172033;margin:0;padding:28px}main{max-width:1120px;margin:auto}textarea{width:100%;min-height:96px;padding:12px;border:1px solid #d7dfeb;border-radius:8px;font:14px monospace;box-sizing:border-box}button,select{background:#fff;color:#245ea8;border:1px solid #cbd9ea;border-radius:7px;padding:8px 14px;cursor:pointer;margin:4px}button.primary{background:#1677ff;color:#fff;border-color:#1677ff}.card{background:#fff;padding:20px;border-radius:14px;box-shadow:0 2px 12px #dfe6f0;margin-bottom:18px}.muted{color:#667085}.history button{background:#f4f8ff;color:#1459b8;margin:4px;padding:9px 12px;text-align:left}.result-box{font:13px/1.55 Consolas,"Microsoft YaHei",monospace;white-space:pre-wrap;background:#fff;border:1px solid #dfe7f1;border-radius:9px;padding:16px}.raw{margin-top:14px;border-top:1px solid #e4eaf2;padding-top:10px}.raw pre{white-space:pre-wrap;max-height:420px;overflow:auto;background:#172033;color:#e8f0ff;padding:14px;border-radius:8px}</style></head>
<body><main><h1>命令控制台</h1><p class="muted">执行宿主 Telegram 操作并查看原始步骤日志。每行一条命令，支持固定白名单命令。</p>
<section class="card"><div style="display:flex;gap:10px;flex-wrap:wrap;align-items:end"><label>群组操作<select id="action"></select></label><div id="params"></div><button id="make">加入流程</button></div><p class="muted">必填参数完整后才能加入；可连续添加多个群组动作。</p><textarea id="command" placeholder="例如：group.list account=2 refresh=true"></textarea><br><button class="primary" id="run">执行流程</button></section>
<section class="card"><h2>运行结果</h2><div id="output" class="result">等待执行…</div></section>
<section class="card history"><div style="display:flex;justify-content:space-between;align-items:center"><h2>历史运行</h2><div><button id="selectAll">全选</button><button id="clearSelected">清除所选</button><button id="clearAll">清空全部</button></div></div><div id="history">加载中…</div></section></main>
<script>
const base='/api/panel/extensions/command-console'; const out=document.getElementById('output');
const CATALOG=[['group.list','查询群组列表',[['account','执行账号','',true],['refresh','刷新','true',false]]],['group.get','查询群组详情',[['account','执行账号','',true],['group','群组 ID','',true]]],['group.invite','邀请客户进群',[['account','执行账号','',true],['group','群组 ID','',true],['usernames','客户列表','',true],['delay','间隔毫秒','2000',false]]],['engagement.group.inspect','检查可复用群组',[['accountId','执行账号 ID','',true],['telegramGroupId','Telegram 群组 ID','',true]]],['engagement.group.available','查询可用群组',[['accountId','执行账号 ID','',true]]],['engagement.group.cleanup','清理执行账号群组',[['accountId','执行账号 ID','',true],['telegramGroupId','Telegram 群组 ID','',true]]],['engagement.account.health','检查执行账号',[['accountId','执行账号 ID','',true]]],['engagement.batch.preview','预览客户邀请批次',[['accountId','执行账号 ID','',true],['telegramGroupId','Telegram 群组 ID','',true],['customerCategoryId','客户分类 ID','',false],['customerLimit','客户数量','3',false]]],['engagement.batch.run','执行客户邀请批次',[['accountId','执行账号 ID','',true],['telegramGroupId','Telegram 群组 ID','',true],['customerCategoryId','客户分类 ID','',false],['inviteCount','邀请数量','3',true],['minSuccess','最低成功数','1',true],['delayMin','最小间隔秒','30',true],['delayMax','最大间隔秒','60',true]]]];
function refreshCatalog(){const sel=document.getElementById('action');sel.innerHTML=CATALOG.map(x=>'<option value="'+x[0]+'">'+x[1]+'</option>').join('');renderParams()}
let accountOptions=[],groupOptions=[],customerOptions=[];
function opts(list,key,label){return '<option value="">请选择</option>'+list.map(x=>'<option value="'+esc(x[key])+'">'+esc(label(x))+'</option>').join('')}
function renderParams(){const a=document.getElementById('action').value,x=CATALOG.find(y=>y[0]===a);document.getElementById('params').innerHTML=(x?.[2]||[]).map(p=>{let control=(p[0]==='account'||p[0]==='accountId')?'<select data-param="'+p[0]+'" data-required="'+p[3]+'">'+opts(accountOptions,'id',x=>'#'+x.id+' '+(x.phone||x.username||x.nickname||''))+'</select>':(p[0]==='group'||p[0]==='telegramGroupId')?'<select data-param="'+p[0]+'" data-required="'+p[3]+'">'+opts(groupOptions,'telegramId',x=>(x.title||'群组')+' · '+x.telegramId)+'</select>':p[0]==='customerCategoryId'?'<select data-param="customerCategoryId" data-required="'+p[3]+'">'+opts(customerOptions,'id',x=>'#'+x.id+' '+x.name)+'</select>':'<input data-param="'+p[0]+'" data-required="'+p[3]+'" value="'+p[2]+'" style="width:140px;padding:8px;border:1px solid #d7dfeb;border-radius:7px">';return '<label>'+p[1]+(p[3]?' *':'')+control+'</label>'}).join('')}
async function loadChoices(){try{const a=await(await fetch('/api/panel/operation-accounts')).json();accountOptions=Array.isArray(a)?a:(a.items||[])}catch{}try{const g=await(await fetch('/api/panel/groups')).json();groupOptions=Array.isArray(g)?g:(g.items||g.data||[])}catch{}try{const c=await(await fetch('/api/panel/customer-groups')).json();customerOptions=Array.isArray(c)?c:(c.items||c.data||[])}catch{}renderParams()}
document.getElementById('action').onchange=renderParams;refreshCatalog();loadChoices();
const EVENT={'run.started':'命令开始','command.parsed':'命令解析','run.context':'运行上下文','step.started':'步骤开始','step.succeeded':'步骤成功','step.failed':'步骤失败','run.succeeded':'命令成功','run.failed':'命令失败'};
const ACTION={'group.list':'群组列表','group.get':'群组详情','group.invite':'邀请用户进群','account.sync':'账号同步','chat.join':'加入群组或频道','proxy.list':'代理列表','engagement.group.inspect':'检查可复用群组','engagement.group.available':'查询可用群组','engagement.group.cleanup':'清理执行账号群组','engagement.account.health':'检查执行账号','engagement.batch.preview':'预览客户邀请批次','engagement.batch.run':'执行客户邀请批次'};
const FIELD={id:'ID',accountId:'账号 ID',accountCategoryId:'账号分类 ID',customerId:'客户 ID',customerCategoryId:'客户分类 ID',groupId:'群组 ID',telegramGroupId:'Telegram 群组 ID',telegramUserId:'Telegram 用户 ID',accessHash:'访问哈希',title:'名称',username:'用户名',displayName:'显示名称',memberCount:'成员数',about:'简介',creatorAccountId:'创建者账号',isCreator:'是否创建者',isAdmin:'是否管理员',createdAt:'创建时间',syncedAt:'同步时间',isPublic:'是否公开',link:'链接',proxyId:'代理 ID',proxyMode:'代理模式',proxyType:'代理类型',proxyHost:'代理地址',proxyPort:'代理端口',proxyStatus:'代理状态',proxySummary:'网络出口',moduleVersion:'模块版本',account:'账号',refresh:'刷新',reusable:'是否可复用',reason:'判定原因',customerMemberCount:'客户成员数',unknownMemberCount:'未识别成员数',successCount:'成功数',minSuccess:'最低成功数',classification:'失败分类'};
const esc=x=>String(x??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c])); const fmt=x=>new Date(x).toLocaleString('zh-CN',{hour12:false});
const ENUM={confirmed:'确认成功',customer_failure:'客户级失败',account_failure:'账号级失败',suspected_account:'疑似账号级失败',transient:'系统/网络暂时失败',unknown_failure:'未分类失败'};
let mode='auto';
function value(v,k){if(v===null||v===undefined||v==='')return '';if(k==='link'&&String(v).startsWith('http'))return '<a href="'+esc(v)+'" target="_blank">'+esc(v)+'</a>';if(typeof v==='boolean')return v?'是':'否';if(typeof v==='object')return esc(JSON.stringify(v));return esc(v)}
function record(o){return '<div class="result"><div class="kv">'+Object.entries(o||{}).filter(([k,v])=>v!==null&&v!==undefined&&v!=='').map(([k,v])=>'<b>'+esc(FIELD[k]||k)+'</b><span>'+value(v,k)+'</span>').join('')+'</div></div>'}
function dataView(d){if(Array.isArray(d)){if(!d.length)return '<p>结果（0 条）</p>';if(mode==='card'||(mode==='auto'&&d.length===1))return '<p>结果（'+d.length+' 条）</p>'+d.map((x,i)=>'<div class="result"><b>#'+(i+1)+'</b>'+record(x)+'</div>').join('');const keys=[...new Set(d.flatMap(x=>Object.keys(x||{})))].filter(k=>d.some(x=>x?.[k]!==null&&x?.[k]!==undefined&&x?.[k]!==''));return '<p>结果（'+d.length+' 条）</p><div style="overflow:auto"><table style="width:100%;border-collapse:collapse"><thead><tr>'+keys.map(k=>'<th style="text-align:left;padding:10px;border-bottom:1px solid #dbe4ef">'+esc(FIELD[k]||k)+'</th>').join('')+'</tr></thead><tbody>'+d.map(x=>'<tr>'+keys.map(k=>'<td style="padding:10px;border-bottom:1px solid #eef2f7">'+value(x?.[k],k)+'</td>').join('')+'</tr>').join('')+'</tbody></table></div>'}return record(d)}
function textData(d,indent=''){if(d===null||d===undefined||d==='')return '';if(Array.isArray(d))return d.length?d.map((x,i)=>indent+'├─ #'+(i+1)+'\n'+textData(x,indent+'│  ')).join('\n'):indent+'（0 条）';if(typeof d==='object')return Object.entries(d).filter(([k,v])=>v!==null&&v!==undefined&&v!=='').map(([k,v])=>{const label=FIELD[k]||k;if(typeof v==='object')return indent+label+'：\n'+textData(v,indent+'  ');const shown=ENUM[String(v)]||((typeof v==='boolean')?(v?'是':'否'):String(v));return indent+label+'：'+shown}).join('\n');return ENUM[String(d)]||String(d)}
function render(events){window.lastEvents=events;const p=events.find(x=>x.type==='command.parsed'),c=events.find(x=>x.type==='run.context'),d=events.find(x=>x.type==='run.succeeded'||x.type==='run.failed');const args=p?.data?.args||{};const actor=args.account||args.accountId||'';let s='┌────────────────────────────────────────────\n';s+='│ '+(ACTION[p?.data?.action]||p?.data?.action||'命令')+'\n';s+='├────────────────────────────────────────────\n';s+='│ ▶ 命令开始　'+fmt(events[0]?.timeUtc)+'\n';s+='│ · 命令解析　'+(ACTION[p?.data?.action]||p?.data?.action||'')+(actor?'　(accountId='+actor+')':'')+'\n';s+='│ · 网络出口　'+(c?.data?.proxySummary||'未取得')+'\n';for(const e of events.filter(x=>x.type==='step.started'||x.type==='step.succeeded'||x.type==='step.failed')){s+='│ '+(e.type==='step.failed'?'✖':e.type==='step.succeeded'?'✔':'▶')+' '+(EVENT[e.type]||e.type)+'　'+fmt(e.timeUtc)+(e.message?'　'+e.message:'')+'\n';if(e.type==='step.succeeded'&&e.data)s+='│ 结果：\n'+textData(e.data)+'\n'}s+='│ '+(d?.type==='run.succeeded'?'✔ 命令成功':'✖ 命令失败')+'\n└────────────────────────────────────────────';out.innerHTML='<div class="result-box">'+esc(s)+'</div><details class="raw"><summary>查看原始 JSON</summary><pre>'+esc(JSON.stringify(events,null,2))+'</pre></details>'}
async function openRun(id){render(await (await fetch(base+'/runs/'+id)).json())}
async function load(){const ids=await (await fetch(base+'/runs')).json(),box=document.getElementById('history');if(!ids.length){box.innerHTML='暂无记录';return}const rows=await Promise.all(ids.map(async id=>({id,e:await(await fetch(base+'/runs/'+id)).json()})));box.innerHTML=rows.map(x=>{const p=x.e.find(e=>e.type==='command.parsed'),d=x.e.find(e=>e.type==='run.succeeded'||e.type==='run.failed');return '<div><input type="checkbox" data-check="'+esc(x.id)+'"><button data-id="'+esc(x.id)+'">'+fmt(x.e[0]?.timeUtc)+' · '+esc(ACTION[p?.data?.action]||p?.data?.action||'群组命令')+' · '+(d?.type==='run.succeeded'?'成功':'失败')+'</button></div>'}).join('');box.querySelectorAll('[data-id]').forEach(b=>b.onclick=()=>openRun(b.dataset.id))}
document.getElementById('selectAll').onclick=()=>document.querySelectorAll('[data-check]').forEach(x=>x.checked=true);
document.getElementById('clearSelected').onclick=async()=>{for(const x of document.querySelectorAll('[data-check]:checked'))await fetch(base+'/runs/'+encodeURIComponent(x.dataset.check),{method:'DELETE'});load()};
document.getElementById('clearAll').onclick=async()=>{if(confirm('确认清空全部运行记录？')){await fetch(base+'/runs',{method:'DELETE'});load();out.textContent='已清空运行记录'}};
document.getElementById('run').onclick=async()=>{const commands=document.getElementById('command').value.split(/\r?\n/).map(x=>x.trim()).filter(Boolean);if(!commands.length)return;out.textContent='执行流程中…';let all=[];for(const command of commands){const r=await fetch(base+'/runs',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({command})});const x=await r.json();all=all.concat(x.events||[])}render(all);load()};load();
document.getElementById('make').onclick=()=>{const inputs=[...document.querySelectorAll('[data-param]')],missing=inputs.find(x=>x.dataset.required==='true'&&!x.value.trim());if(missing){alert('请填写必填参数：'+missing.closest('label').childNodes[0].textContent);missing.focus();return}const a=document.getElementById('action').value,args=inputs.map(x=>x.value?x.dataset.param+'='+((x.dataset.param==='usernames'?'"':'')+x.value+(x.dataset.param==='usernames'?'"':'')):null).filter(Boolean),area=document.getElementById('command');area.value+=(area.value?'\n':'')+a+(args.length?' '+args.join(' '):'')};
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
            Add("run.started", new { moduleVersion = "1.1.0" });
            Add("command.parsed", command);
            var account = command.TryGetInt("account", out var legacyAccount)
                ? legacyAccount
                : command.TryGetInt("accountId", out var formalAccount) ? formalAccount : 0;
            var route = account > 0
                ? await services.GetRequiredService<IAccountProxyResolver>().ResolveAsync(account, ct)
                : null;
            Add("run.context", route?.Proxy is null
                ? new { accountId = account, proxyMode = "direct", proxySummary = account > 0 ? "直连" : "未指定执行账号" }
                : new
                {
                    accountId = account,
                    proxyMode = "proxy",
                    proxyId = route!.Proxy!.ProxyId,
                    proxyType = route.Proxy.Kind,
                    proxyProtocol = route.Proxy.Protocol,
                    proxySummary = $"#{route.Proxy.ProxyId} {route.Proxy.Name} {route.Proxy.Protocol}://{route.Proxy.Host}:{route.Proxy.Port}"
                });
            Add("step.started", new { step = command.Action });
            object? result = command.Action switch
            {
                "account.sync" => await services.GetRequiredService<DataSyncService>().SyncAccountAsync(account, ct),
                "account.list" => await services.GetRequiredService<AccountManagementService>().GetAllAccountsAsync(),
                "account.get" => await services.GetRequiredService<AccountManagementService>().GetAccountAsync(account),
                "chat.join" => await services.GetRequiredService<AccountTelegramToolsService>().JoinChatOrChannelAsync(account, command.Require("target"), ct),
                "group.list" => await services.GetRequiredService<IGroupService>().GetVisibleGroupsAsync(account, ct),
                "group.get" => await services.GetRequiredService<IGroupService>().GetGroupInfoAsync(account, long.Parse(command.Require("group"))),
                "group.invite" => await InviteAsync(command, services, account, ct),
                "engagement.group.inspect" => await EngagementCommands.InspectGroupAsync(command, services, ct),
                "engagement.group.available" => await EngagementCommands.AvailableGroupsAsync(command, services, ct),
                "engagement.group.cleanup" => await EngagementCommands.CleanupGroupAsync(command, services, ct),
                "engagement.account.health" => await EngagementCommands.AccountHealthAsync(command, services, ct),
                "engagement.batch.preview" => await EngagementCommands.PreviewAsync(command, services, ct),
                "engagement.batch.run" => await EngagementCommands.RunAsync(command, services, ct),
                "proxy.list" => await services.GetRequiredService<ProxyManagementService>().ListAsync(ct),
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

internal sealed class EngagementCoordinator
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _locks = new(StringComparer.Ordinal);
    public bool TryAcquire(string kind, string id, string runId) => _locks.TryAdd($"{kind}:{id}", runId);
    public void Release(string kind, string id, string runId) => _locks.TryRemove(new KeyValuePair<string, string>($"{kind}:{id}", runId));
    public string? Owner(string kind, string id) => _locks.TryGetValue($"{kind}:{id}", out var owner) ? owner : null;
}

internal static class EngagementCommands
{
    public static async Task<object> CleanupGroupAsync(ParsedCommand command, IServiceProvider services, CancellationToken ct)
    {
        var accountId = command.RequireInt("accountId");
        var groupId = GetTelegramGroupId(command);
        var snapshot = await services.GetRequiredService<IGroupService>().GetGroupMembershipSnapshotAsync(accountId, groupId, ct);
        var db = services.GetRequiredService<AppDbContext>();
        var customerCount = await db.Customers.AsNoTracking().CountAsync(x => x.TelegramUserId.HasValue && snapshot.MemberUserIds.Contains(x.TelegramUserId.Value), ct);
        if (customerCount > 0)
            throw new CommandException($"群组仍有 {customerCount} 个客户成员，禁止执行清理退出");
        var left = await services.GetRequiredService<IGroupService>().LeaveGroupAsync(accountId, groupId);
        return new { accountId, telegramGroupId = groupId, memberCount = snapshot.MemberCount, customerMemberCount = customerCount, leftGroup = left, status = left ? "清理完成" : "退出失败" };
    }

    public static async Task<object> InspectGroupAsync(ParsedCommand command, IServiceProvider services, CancellationToken ct)
    {
        var accountId = command.RequireInt("accountId");
        var groupId = GetTelegramGroupId(command);
        var groupService = services.GetRequiredService<IGroupService>();
        var group = await groupService.GetGroupInfoAsync(accountId, groupId)
            ?? throw new CommandException($"账号 {accountId} 看不到群组 {groupId}");
        var snapshot = await groupService.GetGroupMembershipSnapshotAsync(accountId, groupId, ct);
        var db = services.GetRequiredService<AppDbContext>();
        var accountIds = await db.Accounts.AsNoTracking().Where(x => snapshot.MemberUserIds.Contains(x.UserId)).Select(x => x.Id).ToListAsync(ct);
        var customerIds = await db.Customers.AsNoTracking().Where(x => x.TelegramUserId.HasValue && snapshot.MemberUserIds.Contains(x.TelegramUserId.Value)).Select(x => x.Id).ToListAsync(ct);
        var knownIds = await db.Accounts.AsNoTracking().Where(x => x.UserId > 0).Select(x => x.UserId).ToListAsync(ct);
        knownIds.AddRange(await db.Customers.AsNoTracking().Where(x => x.TelegramUserId.HasValue).Select(x => x.TelegramUserId!.Value).ToListAsync(ct));
        var unknownCount = snapshot.MemberUserIds.Count(x => !knownIds.Contains(x));
        return new
        {
            groupId = group.Id,
            telegramGroupId = group.TelegramId,
            title = group.Title,
            creatorAccountId = group.CreatorAccountId,
            memberCount = snapshot.MemberCount,
            accountMemberCount = accountIds.Count,
            customerMemberCount = customerIds.Count,
            unknownMemberCount = unknownCount,
            reusable = customerIds.Count == 0 && unknownCount == 0,
            reason = customerIds.Count > 0 ? "群内存在客户" : unknownCount > 0 ? "群内存在未识别成员" : "仅存在已知账号成员",
            memberAccountIds = accountIds,
            memberCustomerIds = customerIds
        };
    }

    public static async Task<object> AvailableGroupsAsync(ParsedCommand command, IServiceProvider services, CancellationToken ct)
    {
        var accountId = command.RequireInt("accountId");
        var db = services.GetRequiredService<AppDbContext>();
        var groups = await db.Groups.AsNoTracking().Where(x => x.CreatorAccountId.HasValue).OrderBy(x => x.Id).ToListAsync(ct);
        List<TelegramPanel.Core.Models.GroupInfo> visible;
        try { visible = await services.GetRequiredService<IGroupService>().GetVisibleGroupsAsync(accountId, ct); }
        catch { visible = new List<TelegramPanel.Core.Models.GroupInfo>(); }
        var visibleIds = visible.Select(x => x.TelegramId).ToHashSet();
        var results = new List<object>();
        foreach (var group in groups)
        {
            try
            {
                var info = await InspectGroupAsync(new ParsedCommand("engagement.group.inspect", new Dictionary<string, string>
                {
                    ["accountId"] = accountId.ToString(), ["telegramGroupId"] = group.TelegramId.ToString()
                }), services, ct);
                var json = System.Text.Json.JsonSerializer.SerializeToElement(info);
                var clean = json.GetProperty("reusable").GetBoolean();
                var alreadyMember = visibleIds.Contains(group.TelegramId);
                results.Add(new
                {
                    groupId = group.Id,
                    telegramGroupId = group.TelegramId,
                    title = group.Title,
                    creatorAccountId = group.CreatorAccountId,
                    reusable = clean && !alreadyMember,
                    currentAccountMember = alreadyMember,
                    reason = alreadyMember ? "执行账号已在群内" : json.GetProperty("reason").GetString(),
                    memberCount = json.GetProperty("memberCount").GetInt32(),
                    customerMemberCount = json.GetProperty("customerMemberCount").GetInt32(),
                    unknownMemberCount = json.GetProperty("unknownMemberCount").GetInt32()
                });
            }
            catch (Exception ex)
            {
                results.Add(new { groupId = group.Id, telegramGroupId = group.TelegramId, reusable = false, reason = ex.Message });
            }
        }
        return results;
    }

    public static async Task<object> AccountHealthAsync(ParsedCommand command, IServiceProvider services, CancellationToken ct)
    {
        var accountId = command.RequireInt("accountId");
        var account = await services.GetRequiredService<AccountManagementService>().GetAccountAsync(accountId)
            ?? throw new CommandException($"账号 {accountId} 不存在");
        var route = await services.GetRequiredService<IAccountProxyResolver>().ResolveAsync(accountId, ct);
        return new
        {
            accountId = account.Id,
            telegramUserId = account.UserId,
            displayNumber = account.DisplayNumber,
            phone = account.DisplayPhone,
            nickname = account.Nickname,
            username = account.Username,
            active = account.IsActive,
            status = account.TelegramStatusSummary ?? (account.IsActive ? "正常" : "未启用"),
            lastSyncAt = account.LastSyncAt,
            apiId = account.ApiId,
            proxySummary = route.Proxy is null ? "直连" : $"#{route.Proxy.ProxyId} {route.Proxy.Name} {route.Proxy.Protocol}://{route.Proxy.Host}:{route.Proxy.Port}"
        };
    }

    public static async Task<object> PreviewAsync(ParsedCommand command, IServiceProvider services, CancellationToken ct)
    {
        var accountId = command.RequireInt("accountId");
        var groupId = GetTelegramGroupId(command);
        var categoryId = command.TryGetInt("customerCategoryId", out var parsedCategory) ? parsedCategory : (int?)null;
        var limit = command.TryGetInt("customerLimit", out var parsedLimit) ? Math.Clamp(parsedLimit, 1, 1000) : 3;
        var inspected = await InspectGroupAsync(command, services, ct);
        var db = services.GetRequiredService<AppDbContext>();
        var query = db.Customers.AsNoTracking().Where(x => !x.IsDeleted && x.TelegramUserId.HasValue && x.InteractionStatus != "contacted");
        if (categoryId.HasValue) query = query.Where(x => x.GroupAssignments.Any(g => g.CustomerGroupId == categoryId.Value));
        var customers = await query.OrderBy(x => x.Id).Take(limit).Select(x => new { x.Id, x.TelegramUserId, x.Username, x.DisplayName, x.InteractionStatus }).ToListAsync(ct);
        return new { accountId, groupId, inspected, customerCategoryId = categoryId, customerLimit = limit, eligibleCustomers = customers };
    }

    public static async Task<object> RunAsync(ParsedCommand command, IServiceProvider services, CancellationToken ct)
    {
        var accountId = command.RequireInt("accountId");
        var groupId = GetTelegramGroupId(command);
        var minSuccess = command.TryGetInt("minSuccess", out var min) ? Math.Max(1, min) : 1;
        var limit = command.TryGetInt("inviteCount", out var count) ? Math.Clamp(count, 1, 100) : 3;
        var minDelay = command.TryGetInt("delayMin", out var low) ? low : 30;
        var maxDelay = command.TryGetInt("delayMax", out var high) ? high : 60;
        if (minDelay < 30 || maxDelay < minDelay || maxDelay > 600)
            throw new CommandException("delayMin/delayMax 必须是 30～600 秒的有效区间");
        var coordinator = services.GetRequiredService<EngagementCoordinator>();
        var runId = Guid.NewGuid().ToString("N");
        if (!coordinator.TryAcquire("account", accountId.ToString(), runId)) throw new CommandException($"账号 {accountId} 已被其他批次占用");
        if (!coordinator.TryAcquire("group", groupId.ToString(), runId)) { coordinator.Release("account", accountId.ToString(), runId); throw new CommandException($"群组 {groupId} 已被其他批次占用"); }
        var joined = false;
        try
        {
            var db = services.GetRequiredService<AppDbContext>();
            var storedGroup = await db.Groups.AsNoTracking().FirstOrDefaultAsync(x => x.TelegramId == groupId, ct);
            var inspectAccountId = storedGroup?.CreatorAccountId ?? accountId;
            var inspectCommand = new ParsedCommand(command.Action, new Dictionary<string, string>(command.Args)
            {
                ["accountId"] = inspectAccountId.ToString(), ["telegramGroupId"] = groupId.ToString()
            });
            var inspected = await InspectGroupAsync(inspectCommand, services, ct);
            var inspectedJson = System.Text.Json.JsonSerializer.SerializeToElement(inspected);
            if (inspectedJson.GetProperty("reusable").GetBoolean() == false)
                throw new CommandException("群组不满足干净群条件，已阻止进入邀请阶段：" + inspectedJson.GetProperty("reason").GetString());
            if (inspectAccountId != accountId)
            {
                var link = await services.GetRequiredService<IGroupService>().ExportJoinLinkAsync(inspectAccountId, groupId);
                var join = await services.GetRequiredService<AccountTelegramToolsService>().JoinChatOrChannelAsync(accountId, link, ct);
                if (!join.Success) throw new CommandException($"执行账号 {accountId} 加入群组失败：{join.Error}");
                joined = true;
            }
            var query = db.Customers.Where(x => !x.IsDeleted && x.TelegramUserId.HasValue && x.InteractionStatus != "contacted");
            if (command.TryGetInt("customerCategoryId", out var categoryId)) query = query.Where(x => x.GroupAssignments.Any(g => g.CustomerGroupId == categoryId));
            var customers = await query.OrderBy(x => x.Id).Take(limit).ToListAsync(ct);
            var results = new List<object>();
            var suspectedFailures = 0;
            foreach (var customer in customers)
            {
                ct.ThrowIfCancellationRequested();
                var target = !string.IsNullOrWhiteSpace(customer.Username) ? "@" + customer.Username.TrimStart('@') : customer.Phone ?? customer.TelegramUserId!.Value.ToString();
                var result = await services.GetRequiredService<IGroupService>().InviteUserAsync(accountId, groupId, target);
                var confirmed = false;
                if (result.Success && result.UserId.HasValue)
                    confirmed = (await services.GetRequiredService<IGroupService>().ConfirmGroupMemberAsync(accountId, groupId, result.UserId.Value, ct)).Confirmed;
                var classification = result.Success && confirmed ? "confirmed" : ClassifyInviteFailure(result.Error, result.Success, confirmed);
                var confirmedSuccess = result.Success && confirmed;
                if (classification == "suspected_account") suspectedFailures++;
                results.Add(new { customerId = customer.Id, telegramUserId = customer.TelegramUserId, target, success = confirmedSuccess, classification, error = result.Error, userId = result.UserId });
                // 邀请确认不等于“已沟通”。活跃消息规则接入后，只有达到 configured milestone 才更新客户状态。
                await db.SaveChangesAsync(ct);
                if (customers.IndexOf(customer) < customers.Count - 1) await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(minDelay, maxDelay + 1)), ct);
                if (suspectedFailures >= 3) break;
            }
            var successCount = results.Count(x => (bool)x.GetType().GetProperty("success")!.GetValue(x)!);
            var leave = joined ? await services.GetRequiredService<IGroupService>().LeaveGroupAsync(accountId, groupId) : false;
            joined = false;
            return new { runId, accountId, groupId, planned = customers.Count, successCount, minSuccess, completed = successCount >= minSuccess, suspectedFailures, leftGroup = leave, results, note = "本版本不自动发送活跃消息；客户标记已沟通仅代表邀请已确认，消息规则接入后将改为按消息完成里程碑写入" };
        }
        finally
        {
            if (joined)
            {
                try { await services.GetRequiredService<IGroupService>().LeaveGroupAsync(accountId, groupId); }
                catch { /* 保留运行结果，清理失败交给后续 group.inspect */ }
            }
            coordinator.Release("group", groupId.ToString(), runId);
            coordinator.Release("account", accountId.ToString(), runId);
        }
    }

    private static string ClassifyInviteFailure(string? error, bool accepted, bool confirmed)
    {
        if (accepted && !confirmed) return "suspected_account";
        var text = error ?? string.Empty;
        if (text.Contains("隐私") || text.Contains("Premium") || text.Contains("已在群") || text.Contains("注销") || text.Contains("不存在")) return "customer_failure";
        if (text.Contains("FROZEN") || text.Contains("PEER_FLOOD") || text.Contains("权限") || text.Contains("Session")) return "account_failure";
        if (text.Contains("超时") || text.Contains("代理") || text.Contains("网络")) return "transient";
        return "unknown_failure";
    }

    private static long GetTelegramGroupId(ParsedCommand command) =>
        command.TryGetLong("telegramGroupId", out var telegramGroupId) ? telegramGroupId : command.RequireLong("groupId");
}

internal sealed class CommandException(string message) : Exception(message);

internal sealed record ParsedCommand(string Action, IReadOnlyDictionary<string, string> Args)
{
    public string Require(string key) => Args.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : throw new CommandException($"缺少参数：{key}");
    public int RequireInt(string key) => int.TryParse(Require(key), out var value) ? value : throw new CommandException($"参数 {key} 必须是整数");
    public long RequireLong(string key) => long.TryParse(Require(key), out var value) ? value : throw new CommandException($"参数 {key} 必须是整数");
    public bool TryGetInt(string key, out int value) => int.TryParse(Args.GetValueOrDefault(key), out value);
    public bool TryGetLong(string key, out long value) => long.TryParse(Args.GetValueOrDefault(key), out value);
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
