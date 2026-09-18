using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TelegramPanel.Core.BatchTasks;
using TelegramPanel.Core.Interfaces;
using TelegramPanel.Core.Services;
using TelegramPanel.Core.Services.Telegram;
using TelegramPanel.Data;
using TelegramPanel.Data.Entities;
using TelegramPanel.Modules;
using System.Collections.Concurrent;

namespace TelegramPanel.Web.Services;

public sealed class CustomerGroupEngagementTaskHandler : IModuleTaskHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    public string TaskType => BatchTaskTypes.CustomerGroupEngagement;

    public async Task ExecuteAsync(IModuleTaskExecutionHost host, CancellationToken cancellationToken)
    {
        var logger = host.Services.GetRequiredService<ILogger<CustomerGroupEngagementTaskHandler>>();
        logger.LogInformation("开始执行批量建群邀请客户任务，任务ID: {TaskId}", host.TaskId);

        var config = JsonSerializer.Deserialize<Config>(host.Config ?? "{}", JsonOptions)
            ?? throw new InvalidOperationException("任务配置解析失败");

        config.AccountIds = config.AccountIds.Where(x => x > 0).Distinct().ToList();
        config.CompletedCustomerIds ??= [];
        config.WorkerCount = Math.Clamp(config.WorkerCount, 1, 10);

        var db = host.Services.GetRequiredService<AppDbContext>();
        async Task WriteTaskLog(string level, string message)
        {
            var text = message.Length > 4000 ? message.Substring(0, 4000) : message;
            db.BatchTaskLogs.Add(new BatchTaskLog { BatchTaskId = host.TaskId, Level = level, Message = text, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(cancellationToken);
        }
        await WriteTaskLog("info", "任务开始执行");
        var taskManagement = host.Services.GetRequiredService<BatchTaskManagementService>();
        var groupService = host.Services.GetRequiredService<IGroupService>();
        var groupManagement = host.Services.GetRequiredService<GroupManagementService>();
        var tools = host.Services.GetRequiredService<AccountTelegramToolsService>();

        // 加载执行账号
        if (config.AccountCategoryId.HasValue)
        {
            config.AccountIds = await db.Accounts.AsNoTracking()
                .Where(x => x.CategoryId == config.AccountCategoryId && x.IsActive && x.TelegramStatusOk != false)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
            logger.LogInformation("从分类 {CategoryId} 加载到 {Count} 个可用执行账号",
                config.AccountCategoryId, config.AccountIds.Count);
            var categoryName = config.AccountCategoryName;
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                categoryName = await db.AccountCategories.AsNoTracking()
                    .Where(x => x.Id == config.AccountCategoryId)
                    .Select(x => x.Name)
                    .FirstOrDefaultAsync(cancellationToken);
            }
            await WriteTaskLog("info", $"从 账号分类:{categoryName ?? "未命名"} (#{config.AccountCategoryId})加载 {config.AccountIds.Count} 个可用执行账号");
        }

        if (config.AccountIds.Count == 0)
        {
            var scope = config.AccountCategoryId.HasValue
                ? $"账号分类 #{config.AccountCategoryId.Value} 中没有启用且 Telegram 状态可用的执行账号"
                : "没有启用且 Telegram 状态可用的执行账号";
            logger.LogError("{Message}", scope);
            throw new InvalidOperationException(scope);
        }

        // 加载客户，按客户分类统计
        var query = db.Customers.Include(x => x.GroupAssignments)
            .Where(x => !config.CompletedCustomerIds.Contains(x.Id));
        if (!config.ForceRecontact)
            query = query.Where(x => x.InteractionStatus != "contacted");

        if (config.CustomerGroupIds.Count > 0)
        {
            query = query.Where(x => x.GroupAssignments.Any(g => config.CustomerGroupIds.Contains(g.CustomerGroupId)));
            logger.LogInformation("筛选客户分类: {GroupIds}", string.Join(", ", config.CustomerGroupIds));
        }

        var customers = await query.OrderBy(x => x.Id).ToListAsync(cancellationToken);
        var contactedQuery = db.Customers.AsNoTracking().Where(x => x.InteractionStatus == "contacted");
        if (config.CustomerGroupIds.Count > 0)
            contactedQuery = contactedQuery.Where(x => x.GroupAssignments.Any(g => config.CustomerGroupIds.Contains(g.CustomerGroupId)));
        var contactedCount = await contactedQuery.CountAsync(cancellationToken);

        if (config.AssignmentMode == "random")
        {
            customers = customers.OrderBy(_ => Guid.NewGuid()).ToList();
            logger.LogInformation("使用随机分配模式");
        }

        logger.LogInformation("加载到 {Count} 个待邀请客户，已沟通跳过 {Contacted}", customers.Count, contactedCount);
        await WriteTaskLog("info", $"待邀请 {customers.Count} 人，已沟通跳过 {contactedCount} 人");

        if (customers.Count == 0)
        {
            var msg = config.ForceRecontact
                ? "没有可执行客户：所选客户分类为空。"
                : contactedCount > 0
                    ? $"没有待邀请客户：所选分类里 {contactedCount} 人全部已是「已沟通」，任务不会重复建群邀请。请先在客户列表改回未执行，勾选强制二次确认，或换一个还有未沟通客户的分类。"
                    : "没有待邀请客户：所选客户分类为空。";
            logger.LogWarning("{Message}", msg);
            await WriteTaskLog("error", msg);
            throw new InvalidOperationException(msg);
        }

        // 按客户分类统计数量
        var customersByGroup = new Dictionary<int, List<Customer>>();
        foreach (var customer in customers)
        {
            foreach (var assignment in customer.GroupAssignments)
            {
                if (config.CustomerGroupIds.Count == 0 || config.CustomerGroupIds.Contains(assignment.CustomerGroupId))
                {
                    if (!customersByGroup.ContainsKey(assignment.CustomerGroupId))
                    {
                        customersByGroup[assignment.CustomerGroupId] = [];
                    }
                    customersByGroup[assignment.CustomerGroupId].Add(customer);
                }
            }
        }

        // 记录每个客户分类的数量
        foreach (var kvp in customersByGroup)
        {
            var groupName = await db.CustomerGroups
                .Where(x => x.Id == kvp.Key)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? $"分类#{kvp.Key}";
            logger.LogInformation("客户分类 {GroupName} ({GroupId}): {Count} 人",
                groupName, kvp.Key, kvp.Value.Count);
        }

        var completed = config.CompletedCustomerIds.Count;
        var failed = 0;
        var perGroup = Math.Clamp(config.CustomersPerGroup, 1, 200);
        var healthyAccounts = new ConcurrentBag<int>(config.AccountIds);
        var accountLock = new SemaphoreSlim(config.WorkerCount, config.WorkerCount);
        var progressLock = new object();

        logger.LogInformation("开始批量建群，每群 {PerGroup} 人，共 {TotalGroups} 个群，并发数 {WorkerCount}",
            perGroup, (customers.Count + perGroup - 1) / perGroup, config.WorkerCount);

        // 将客户分成多个批次
        var batches = new List<List<Customer>>();
        for (var offset = 0; offset < customers.Count; offset += perGroup)
        {
            batches.Add(customers.Skip(offset).Take(perGroup).ToList());
        }

        // 并发处理每个批次
        await Parallel.ForEachAsync(batches.Select((chunk, index) => (chunk, index)),
            new ParallelOptions { MaxDegreeOfParallelism = config.WorkerCount, CancellationToken = cancellationToken },
            async (item, ct) =>
            {
                var (chunk, index) = item;
                var groupSeq = index + 1;

                if (!await host.IsStillRunningAsync(ct))
                {
                    logger.LogWarning("任务已停止");
                    return;
                }

                if (healthyAccounts.IsEmpty)
                {
                    logger.LogError("所有执行账号均已失效");
                    return;
                }

                await accountLock.WaitAsync(ct);
                int accountId;
                try
                {
                    if (!healthyAccounts.TryTake(out accountId))
                    {
                        logger.LogError("无法获取可用执行账号");
                        return;
                    }
                }
                finally
                {
                    accountLock.Release();
                }

                var templateRendering = host.Services.GetRequiredService<TemplateRenderingService>();
                var dispatcher = host.Services.GetRequiredService<EngagementMessageDispatchService>();
                // seq/date 是任务内置变量，先展开后再渲染用户选择的字典变量。
                // 这样 {seq} 不会被误当成名为 seq 的文本字典而重复随机取值。
                var rawTitle = (config.GroupTitleTemplate ?? "Group {seq}")
                    .Replace("{seq}", groupSeq.ToString())
                    .Replace("{date}", DateTime.Now.ToString("yyyyMMdd"));
                var titleResult = await templateRendering.RenderTextTemplateDetailedAsync(rawTitle, ct);
                var title = string.IsNullOrWhiteSpace(titleResult.Text) ? rawTitle : titleResult.Text;
                var aboutRaw = config.GroupAboutTemplate ?? string.Empty;
                var about = string.IsNullOrWhiteSpace(aboutRaw) ? string.Empty : await templateRendering.RenderTextTemplateAsync(aboutRaw, ct);
                if (titleResult.Picks.Count > 0)
                {
                    var pick = titleResult.Picks[0];
                    await WriteTaskLog("info", "创建群组动作 加载字典 " + pick.DictionaryName + " 随机值" + pick.Index + " 群名为" + title);
                }
                else
                {
                    await WriteTaskLog("info", "创建群组动作 加载字典 随机值" + groupSeq + " 群名为" + title);
                }

                logger.LogInformation("准备创建第 {Seq} 个群组，标题: {Title}，使用账号 {AccountId}，邀请 {Count} 个客户",
                    groupSeq, title, accountId, chunk.Count);

                try
                {
                    var info = await groupService.CreatePrivateGroupAsync(accountId, title, about);
                    logger.LogInformation("成功创建群组 {TelegramId}，标题: {Title}", info.TelegramId, title);

                    var now = DateTime.UtcNow;
                    var saved = await groupManagement.CreateOrUpdateGroupAsync(new Group
                    {
                        TelegramId = info.TelegramId,
                        AccessHash = info.AccessHash,
                        Title = title,
                        About = about,
                        MemberCount = Math.Max(1, info.MemberCount),
                        CreatorAccountId = accountId,
                        CreatedAt = info.CreatedAt ?? now,
                        SystemCreatedAtUtc = now,
                        SyncedAt = now
                    });

                    await groupManagement.UpsertAccountGroupAsync(accountId, saved.Id, true, true, now);
                    await WriteTaskLog("info", "待邀请 " + chunk.Count + " 人");

                    var executor = await db.Accounts.AsNoTracking()
                        .Where(x => x.Id == accountId)
                        .Select(x => new { x.UserId, x.Phone })
                        .FirstOrDefaultAsync(ct);
                    var invited = new List<Customer>();
                    var inviteErrors = new List<string>();
                    var accountIsHealthy = true;
                    var groupFailed = 0;
                    var seenUserIds = new HashSet<long>();

                    foreach (var customer in chunk)
                    {
                        if (!accountIsHealthy)
                        {
                            logger.LogWarning("账号 {AccountId} 已失效，停止邀请当前群的剩余客户", accountId);
                            break;
                        }

                        var label = CustomerLabel(customer);
                        if (executor != null &&
                            ((executor.UserId > 0 && customer.TelegramUserId == executor.UserId) ||
                             (!string.IsNullOrWhiteSpace(executor.Phone) && string.Equals(NormalizePhone(executor.Phone), NormalizePhone(customer.Phone), StringComparison.Ordinal))))
                        {
                            groupFailed++;
                            lock (progressLock) { failed++; }
                            await WriteTaskLog("info", "邀请动作 邀请 " + label + " 进群 失败：目标是当前执行账号自己");
                            continue;
                        }

                        var target = !string.IsNullOrWhiteSpace(customer.Username)
                            ? "@" + customer.Username
                            : customer.Phone;

                        if (string.IsNullOrWhiteSpace(target))
                        {
                            groupFailed++;
                            lock (progressLock) { failed++; }
                            inviteErrors.Add("客户 #" + customer.Id + " 缺少手机号和用户名");
                            await WriteTaskLog("info", "邀请动作 邀请 " + label + " 进群 失败：缺少手机号和用户名");
                            continue;
                        }

                        var result = await InviteWithRetryAsync(groupService, accountId, info.TelegramId, target, config, ct, logger);
                        // 失败结果必须在进入任何成功/成员核验分支前结束处理，
                        // 防止错误结果因 UserId 或旧客户端返回值被误记为成功。
                        if (!result.Success)
                        {
                            groupFailed++;
                            lock (progressLock) { failed++; }
                            var failureReason = result.IsSelf
                                ? "目标是当前执行账号自己"
                                : result.AlreadyInGroup
                                    ? "已在群中，不计入新邀请"
                                    : (string.IsNullOrWhiteSpace(result.Error) ? "Telegram 未返回失败原因" : result.Error);
                            inviteErrors.Add(target + ": " + failureReason);
                            await WriteTaskLog("warning", "邀请动作 邀请 " + label + " 进群 失败：" + failureReason);
                            if (IsAccountFailure(result.Error))
                            {
                                accountIsHealthy = false;
                                await WriteTaskLog("error", "执行账号 " + accountId + " 已移出本次任务：" + result.Error);
                                break;
                            }
                            await Delay(config, ct);
                            continue;
                        }

                        if (result.UserId is > 0 && !seenUserIds.Add(result.UserId.Value) && result.Success)
                        {
                            groupFailed++;
                            lock (progressLock) { failed++; }
                            await WriteTaskLog("info", "邀请动作 邀请 " + label + " 进群 失败：与本群已邀请用户重复");
                            await Delay(config, ct);
                            continue;
                        }

                        if (result.Success && !result.AlreadyInGroup && !result.IsSelf)
                        {
                            // InviteUserAsync 已包含 updates/短重试；这里再做轻量兜底，防止边界竞态。
                            var membership = result.UserId is > 0
                                ? await groupService.ConfirmGroupMemberAsync(accountId, info.TelegramId, result.UserId.Value, ct, maxAttempts: 2, delayMs: 800)
                                : (Confirmed: false, MemberCount: 0, MemberUserIds: Array.Empty<long>());
                            if (result.UserId is not > 0 || !membership.Confirmed)
                            {
                                groupFailed++;
                                lock (progressLock) { failed++; }
                                var verifyError = "Telegram 已接受邀请但短重试后成员快照仍未确认入群";
                                inviteErrors.Add(target + ": " + verifyError);
                                await WriteTaskLog("warning", "邀请动作 邀请 " + label + " 失败：" + verifyError);
                            }
                            else
                            {
                                invited.Add(customer);
                                await WriteTaskLog("info", "邀请动作 邀请 " + (string.IsNullOrWhiteSpace(result.DisplayName) ? label : result.DisplayName) + " 进群 成功（成员已确认）");
                            }
                        }
                        else
                        {
                            groupFailed++;
                            lock (progressLock) { failed++; }
                            var reason = result.IsSelf ? "目标是当前执行账号自己" : (result.AlreadyInGroup ? "已在群中，不计入新邀请" : (result.Error ?? "邀请失败"));
                            inviteErrors.Add(target + ": " + reason);
                            await WriteTaskLog("info", "邀请动作 邀请 " + label + " 进群 失败：" + reason);

                            if (IsAccountFailure(result.Error))
                            {
                                accountIsHealthy = false;
                                logger.LogError("执行账号 {AccountId} 标记为失效: {Error}", accountId, result.Error);
                                break;
                            }
                        }

                        await Delay(config, ct);
                    }

                    if (accountIsHealthy)
                    {
                        healthyAccounts.Add(accountId);
                    }

                    var snapshot = await groupService.GetGroupMembershipSnapshotAsync(accountId, info.TelegramId, ct);
                    saved.MemberCount = snapshot.MemberCount > 0 ? snapshot.MemberCount : Math.Max(1, 1 + invited.Count);
                    saved.SyncedAt = DateTime.UtcNow;
                    await groupManagement.CreateOrUpdateGroupAsync(saved);
                    await WriteTaskLog("info", "群组 " + title + "(" + info.TelegramId + ")  邀请成功 " + invited.Count + " 人，失败 " + groupFailed + " 人，成员数 " + saved.MemberCount);

                    if (invited.Count < Math.Max(1, config.MinSuccessfulInvites))
                    {
                        logger.LogWarning("群组 {TelegramId} 成功邀请人数 {Invited} 不足最小要求 {Min}，跳过活跃消息发送",
                            info.TelegramId, invited.Count, config.MinSuccessfulInvites);
                        await UpdateProgress(host, config, db, taskManagement, completed, failed, ct);
                        return;
                    }

                    var resolved = await tools.ResolveChatTargetAsync(accountId, info.TelegramId.ToString(), ct);
                    if (!resolved.Success || resolved.Target == null)
                    {
                        logger.LogError("无法解析新建群组 {TelegramId}: {Error}", info.TelegramId, resolved.Error);
                        throw new InvalidOperationException(resolved.Error ?? "无法解析新建群组");
                    }

                    var rules = config.MessageRules.Count > 0
                        ? config.MessageRules
                        : config.ActivityMessages.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => new MessageRule { Text = x }).ToList();
                    if (rules.Count == 0) rules.Add(new MessageRule { Text = "Hello" });
                    var ruleIndex = 0;
                    foreach (var rule in rules)
                    {
                        ruleIndex++;
                        await dispatcher.SendRuleAsync(accountId, resolved.Target, rule, "规则" + ruleIndex, msg => WriteTaskLog("info", msg), ct);
                        await Delay(config, ct);
                    }

                    foreach (var customer in invited)
                    {
                        var label = CustomerLabel(customer);
                        lock (progressLock)
                        {
                            customer.InteractionStatus = "contacted";
                            customer.LastInteractionAt ??= DateTime.UtcNow;
                            customer.UpdatedAt = DateTime.UtcNow;
                            if (config.CompletedCustomerIds.Add(customer.Id))
                            {
                                completed++;
                            }
                        }
                        await WriteTaskLog("info", "标记 " + label + " 已沟通");
                    }

                    await db.SaveChangesAsync(ct);
                    await UpdateProgress(host, config, db, taskManagement, completed, failed, ct);

                    logger.LogInformation("完成第 {Seq} 个群组，已完成 {Completed}，失败 {Failed}",
                        groupSeq, completed, failed);                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    logger.LogError(ex, "创建群组或邀请过程中发生异常");
                    await WriteTaskLog("error", "群处理失败: " + ex.Message);

                    // 检查是否是账号级别的失败
                    if (IsAccountFailure(ex.Message))
                    {
                        logger.LogError("执行账号 {AccountId} 因异常标记为失效: {Message}", accountId, ex.Message);
                        // 不将账号放回健康池
                    }
                    else
                    {
                        // 非账号问题，将账号放回池中
                        healthyAccounts.Add(accountId);
                    }

                    // 标记整个分组失败
                    lock (progressLock)
                    {
                        failed += chunk.Count;
                    }

                    await UpdateProgress(host, config, db, taskManagement, completed, failed, ct);
                }
            });

        logger.LogInformation("批量建群邀请任务完成，已完成 {Completed}，失败 {Failed}", completed, failed);
        await WriteTaskLog("info", $"任务结束，完成邀请成功 {completed} 人，失败{failed}人。");

        if (healthyAccounts.IsEmpty)
        {
            logger.LogError("所有执行账号均已失效");
            throw new InvalidOperationException("所有执行账号均已失效");
        }
    }

    private static string CustomerLabel(Customer customer)
    {
        if (!string.IsNullOrWhiteSpace(customer.DisplayName)) return customer.DisplayName.Trim();
        if (!string.IsNullOrWhiteSpace(customer.Nickname)) return customer.Nickname.Trim();
        if (!string.IsNullOrWhiteSpace(customer.Username)) return "@" + customer.Username.Trim().TrimStart('@');
        if (!string.IsNullOrWhiteSpace(customer.Phone)) return customer.Phone.Trim();
        return "#" + customer.Id;
    }

    private static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        return new string(phone.Where(char.IsDigit).ToArray());
    }
    private static async Task UpdateProgress(
        IModuleTaskExecutionHost host,
        Config config,
        AppDbContext db,
        BatchTaskManagementService taskManagement,
        int completed,
        int failed,
        CancellationToken ct)
    {
        try
        {
            await taskManagement.UpdateTaskConfigAsync(host.TaskId, JsonSerializer.Serialize(config, JsonOptions));
            await host.UpdateProgressAsync(completed, failed, ct);
        }
        catch (Exception ex)
        {
            // 进度更新失败不应该中断任务
            var logger = host.Services.GetRequiredService<ILogger<CustomerGroupEngagementTaskHandler>>();
            logger.LogWarning(ex, "更新进度失败");
        }
    }

    private static async Task<InviteResult> InviteWithRetryAsync(
        IGroupService service,
        int accountId,
        long groupId,
        string target,
        Config config,
        CancellationToken ct,
        ILogger logger)
    {
        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var result = await service.InviteUserAsync(accountId, groupId, target);
            var error = result.Error ?? string.Empty;

            // 检查 FloodWait
            if (TryFloodWait(error, out var seconds))
            {
                var waitSeconds = Math.Min(seconds, 3600);
                logger.LogWarning("遇到 FloodWait，需要等待 {Seconds} 秒", waitSeconds);
                await Task.Delay(TimeSpan.FromSeconds(waitSeconds), ct);
                continue; // FloodWait 后重试
            }

            // 检查是否是超时错误
            var isTimeout = error.Contains("TIMEOUT", StringComparison.OrdinalIgnoreCase) ||
                           error.Contains("超时", StringComparison.OrdinalIgnoreCase);

            if (!isTimeout)
            {
                // 不是超时错误，直接返回结果
                return result;
            }

            // 是超时错误，检查是否还有重试机会
            if (attempt < maxAttempts)
            {
                var delaySeconds = Math.Max(1, config.MinDelaySeconds);
                logger.LogWarning("调用超时（第 {Attempt} 次），{DelaySeconds} 秒后重试", attempt, delaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), ct);
            }
            else
            {
                logger.LogError("调用超时，已重试 {MaxAttempts} 次，标记执行账号失败", maxAttempts);
                return new InviteResult(target, false, $"调用超时，已重试 {maxAttempts} 次，执行账号标记为失败");
            }
        }

        return new InviteResult(target, false, $"调用超时，已重试 {maxAttempts} 次");
    }

    private static bool IsAccountFailure(string? error)
    {
        if (string.IsNullOrWhiteSpace(error)) return false;

        var text = error.ToUpperInvariant();

        // 账号冻结或封禁
        if (text.Contains("FROZEN") || text.Contains("账号被冻结")) return true;

        // 会话失效
        if (text.Contains("SESSION") || text.Contains("会话")) return true;

        // 授权密钥失效
        if (text.Contains("AUTH_KEY") || text.Contains("AUTH_RESTART")) return true;

        // 代理/传输链路已断开时，当前 Telegram 客户端不可继续复用；
        // 将该账号移出本次任务健康池，剩余群组继续使用其他账号。
        if (text.Contains("代理连接提前关闭") || text.Contains("PROXY") ||
            text.Contains("CONNECTION CLOSED") || text.Contains("SOCKET CLOSED") ||
            text.Contains("TRANSPORT CLOSED")) return true;

        // 超时（3次重试后）- 由 InviteWithRetryAsync 处理
        // 这里只检查明确的超时失败消息
        if (text.Contains("已重试") && text.Contains("执行账号标记为失败")) return true;

        return false;
    }

    private static bool TryFloodWait(string error, out int seconds)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            error,
            @"FLOOD_WAIT[_ ]?(\d+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return int.TryParse(match.Success ? match.Groups[1].Value : null, out seconds);
    }

    private static Task Delay(Config config, CancellationToken ct)
    {
        var max = Math.Max(config.MinDelaySeconds, config.MaxDelaySeconds);
        var value = max <= 0 ? 0 : Random.Shared.Next(Math.Max(0, config.MinDelaySeconds), max + 1);
        return value == 0 ? Task.CompletedTask : Task.Delay(TimeSpan.FromSeconds(value), ct);
    }

    public sealed class Config
    {
        [JsonPropertyName("account_ids")]
        public List<int> AccountIds { get; set; } = [];

        [JsonPropertyName("account_category_id")]
        public int? AccountCategoryId { get; set; }

        [JsonPropertyName("account_category_name")]
        public string? AccountCategoryName { get; set; }

        [JsonPropertyName("customer_group_ids")]
        public List<int> CustomerGroupIds { get; set; } = [];

        [JsonPropertyName("customer_group_names")]
        public List<string> CustomerGroupNames { get; set; } = [];

        [JsonPropertyName("customers_per_group")]
        public int CustomersPerGroup { get; set; } = 10;

        [JsonPropertyName("assignment_mode")]
        public string AssignmentMode { get; set; } = "queue";

        [JsonPropertyName("worker_count")]
        public int WorkerCount { get; set; } = 1;

        [JsonPropertyName("group_title_template")]
        public string? GroupTitleTemplate { get; set; }

        [JsonPropertyName("group_about_template")]
        public string? GroupAboutTemplate { get; set; }

        [JsonPropertyName("text_dictionary_name")]
        public string? TextDictionaryName { get; set; }

        [JsonPropertyName("image_dictionary_name")]
        public string? ImageDictionaryName { get; set; }

        [JsonPropertyName("activity_messages")]
        public List<string> ActivityMessages { get; set; } = [];

        [JsonPropertyName("message_rules")]
        public List<MessageRule> MessageRules { get; set; } = [];

        [JsonPropertyName("min_delay_seconds")]
        public int MinDelaySeconds { get; set; } = 3;

        [JsonPropertyName("max_delay_seconds")]
        public int MaxDelaySeconds { get; set; } = 8;

        [JsonPropertyName("min_successful_invites")]
        public int MinSuccessfulInvites { get; set; } = 1;

        [JsonPropertyName("completed_customer_ids")]
        public HashSet<int> CompletedCustomerIds { get; set; } = [];

        [JsonPropertyName("force_recontact")]
        public bool ForceRecontact { get; set; }
    }

    public sealed class MessageRule
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("image_dictionary_token")]
        public string? ImageDictionaryToken { get; set; }
        [JsonPropertyName("material_dictionary_token")]
        public string? MaterialDictionaryToken { get; set; }
        [JsonPropertyName("material_device")]
        public string? MaterialDevice { get; set; }
        [JsonPropertyName("material_time_mode")]
        public string? MaterialTimeMode { get; set; }
        [JsonPropertyName("material_time")]
        public string? MaterialTime { get; set; }
        [JsonPropertyName("material_scale")]
        public double MaterialScale { get; set; } = 1;
        [JsonPropertyName("material_notification")]
        public string? MaterialNotification { get; set; }
        [JsonPropertyName("extra_texts")]
        public List<string> ExtraTexts { get; set; } = [];
        [JsonPropertyName("extra_images")]
        public List<MessageRule> ExtraImages { get; set; } = [];
    }
}
