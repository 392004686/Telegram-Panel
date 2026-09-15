using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TelegramPanel.Core.BatchTasks;
using TelegramPanel.Core.Interfaces;
using TelegramPanel.Core.Services;
using TelegramPanel.Core.Services.Telegram;
using TelegramPanel.Data;
using TelegramPanel.Data.Entities;
using TelegramPanel.Modules;

namespace TelegramPanel.Web.Services;

public sealed class CustomerGroupEngagementTaskHandler : IModuleTaskHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    public string TaskType => BatchTaskTypes.CustomerGroupEngagement;

    public async Task ExecuteAsync(IModuleTaskExecutionHost host, CancellationToken cancellationToken)
    {
        var config = JsonSerializer.Deserialize<Config>(host.Config ?? "{}", JsonOptions) ?? throw new InvalidOperationException("任务配置解析失败");
        config.AccountIds = config.AccountIds.Where(x => x > 0).Distinct().ToList(); config.CompletedCustomerIds ??= [];
        var db = host.Services.GetRequiredService<AppDbContext>();
        var taskManagement = host.Services.GetRequiredService<BatchTaskManagementService>();
        var groupService = host.Services.GetRequiredService<IGroupService>();
        var groupManagement = host.Services.GetRequiredService<GroupManagementService>();
        var tools = host.Services.GetRequiredService<AccountTelegramToolsService>();
        if (config.AccountCategoryId.HasValue) config.AccountIds = await db.Accounts.AsNoTracking().Where(x => x.CategoryId == config.AccountCategoryId && x.IsActive && x.TelegramStatusOk != false).Select(x => x.Id).ToListAsync(cancellationToken);
        if (config.AccountIds.Count == 0) throw new InvalidOperationException("没有可用执行账号");
        var query = db.Customers.Include(x => x.GroupAssignments).Where(x => x.InteractionStatus != "contacted" && !config.CompletedCustomerIds.Contains(x.Id));
        if (config.CustomerGroupIds.Count > 0) query = query.Where(x => x.GroupAssignments.Any(g => config.CustomerGroupIds.Contains(g.CustomerGroupId)));
        var customers = await query.OrderBy(x => x.Id).Take(Math.Clamp(config.CustomerLimit, 1, 10000)).ToListAsync(cancellationToken);
        if (config.AssignmentMode == "random") customers = customers.OrderBy(_ => Guid.NewGuid()).ToList();
        var completed = config.CompletedCustomerIds.Count, failed = 0, perGroup = Math.Clamp(config.CustomersPerGroup, 1, 200);
        for (var offset = 0; offset < customers.Count; offset += perGroup)
        {
            if (!await host.IsStillRunningAsync(cancellationToken)) return;
            var accountId = config.AccountIds[(offset / perGroup) % config.AccountIds.Count];
            var chunk = customers.Skip(offset).Take(perGroup).ToList();
            var title = (config.GroupTitleTemplate ?? "客户沟通群 {seq}").Replace("{seq}", (offset / perGroup + 1).ToString()).Replace("{date}", DateTime.Now.ToString("yyyyMMdd"));
            try
            {
                var info = await groupService.CreatePrivateGroupAsync(accountId, title, config.GroupAboutTemplate ?? string.Empty);
                var now = DateTime.UtcNow;
                var saved = await groupManagement.CreateOrUpdateGroupAsync(new Group { TelegramId = info.TelegramId, AccessHash = info.AccessHash, Title = title, About = config.GroupAboutTemplate, MemberCount = Math.Max(1, info.MemberCount), CreatorAccountId = accountId, CreatedAt = info.CreatedAt ?? now, SystemCreatedAtUtc = now, SyncedAt = now });
                await groupManagement.UpsertAccountGroupAsync(accountId, saved.Id, true, true, now);
                var invited = new List<Customer>();
                foreach (var customer in chunk)
                {
                    var target = !string.IsNullOrWhiteSpace(customer.Username) ? "@" + customer.Username : customer.Phone;
                    if (string.IsNullOrWhiteSpace(target)) { failed++; continue; }
                    var result = await groupService.InviteUserAsync(accountId, info.TelegramId, target);
                    if (result.Success) invited.Add(customer); else failed++;
                    await Delay(config, cancellationToken);
                }
                if (invited.Count < Math.Max(1, config.MinSuccessfulInvites)) { await host.UpdateProgressAsync(completed, failed, cancellationToken); continue; }
                var resolved = await tools.ResolveChatTargetAsync(accountId, info.TelegramId.ToString(), cancellationToken);
                if (!resolved.Success || resolved.Target == null) throw new InvalidOperationException(resolved.Error ?? "无法解析新建群组");
                foreach (var message in config.ActivityMessages.Where(x => !string.IsNullOrWhiteSpace(x)))
                {
                    var sent = await tools.SendMessageToResolvedChatAsync(accountId, resolved.Target, message, cancellationToken: cancellationToken);
                    if (!sent.Success) throw new InvalidOperationException(sent.Error ?? "活跃消息发送失败");
                    await Delay(config, cancellationToken);
                }
                foreach (var customer in invited)
                {
                    customer.InteractionStatus = "contacted"; customer.LastInteractionAt ??= DateTime.UtcNow; customer.UpdatedAt = DateTime.UtcNow;
                    if (config.CompletedCustomerIds.Add(customer.Id)) completed++;
                }
                await db.SaveChangesAsync(cancellationToken); await taskManagement.UpdateTaskConfigAsync(host.TaskId, JsonSerializer.Serialize(config, JsonOptions)); await host.UpdateProgressAsync(completed, failed, cancellationToken);
            }
            catch { failed += chunk.Count; await host.UpdateProgressAsync(completed, failed, cancellationToken); throw; }
        }
    }

    private static Task Delay(Config config, CancellationToken ct) { var max = Math.Max(config.MinDelaySeconds, config.MaxDelaySeconds); var value = max <= 0 ? 0 : Random.Shared.Next(Math.Max(0, config.MinDelaySeconds), max + 1); return value == 0 ? Task.CompletedTask : Task.Delay(TimeSpan.FromSeconds(value), ct); }
    public sealed class Config { public List<int> AccountIds { get; set; } = []; public int? AccountCategoryId { get; set; } public List<int> CustomerGroupIds { get; set; } = []; public int CustomerLimit { get; set; } = 100; public int CustomersPerGroup { get; set; } = 10; public string AssignmentMode { get; set; } = "queue"; public int WorkerCount { get; set; } = 1; public string? GroupTitleTemplate { get; set; } public string? GroupAboutTemplate { get; set; } public List<string> ActivityMessages { get; set; } = []; public int MinDelaySeconds { get; set; } = 3; public int MaxDelaySeconds { get; set; } = 8; public int MinSuccessfulInvites { get; set; } = 1; public HashSet<int> CompletedCustomerIds { get; set; } = []; }
}
