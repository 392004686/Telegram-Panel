using System.Text.Json;
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
            .Where(x => x.InteractionStatus != "contacted" && !config.CompletedCustomerIds.Contains(x.Id));

        if (config.CustomerGroupIds.Count > 0)
        {
            query = query.Where(x => x.GroupAssignments.Any(g => config.CustomerGroupIds.Contains(g.CustomerGroupId)));
            logger.LogInformation("筛选客户分类: {GroupIds}", string.Join(", ", config.CustomerGroupIds));
        }

        var customers = await query.OrderBy(x => x.Id).ToListAsync(cancellationToken);

        if (config.AssignmentMode == "random")
        {
            customers = customers.OrderBy(_ => Guid.NewGuid()).ToList();
            logger.LogInformation("使用随机分配模式");
        }

        logger.LogInformation("加载到 {Count} 个待邀请客户", customers.Count);

        if (customers.Count == 0)
        {
            logger.LogWarning("没有找到待邀请的客户");
            return;
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

                var title = (config.GroupTitleTemplate ?? "客户沟通群 {seq}")
                    .Replace("{seq}", groupSeq.ToString())
                    .Replace("{date}", DateTime.Now.ToString("yyyyMMdd"));

                logger.LogInformation("准备创建第 {Seq} 个群组，标题: {Title}，使用账号 {AccountId}，邀请 {Count} 个客户",
                    groupSeq, title, accountId, chunk.Count);

                try
                {
                    // 创建群组
                    var info = await groupService.CreatePrivateGroupAsync(accountId, title, config.GroupAboutTemplate ?? string.Empty);
                    logger.LogInformation("成功创建群组 {TelegramId}，标题: {Title}", info.TelegramId, title);

                    var now = DateTime.UtcNow;
                    var saved = await groupManagement.CreateOrUpdateGroupAsync(new Group
                    {
                        TelegramId = info.TelegramId,
                        AccessHash = info.AccessHash,
                        Title = title,
                        About = config.GroupAboutTemplate,
                        MemberCount = Math.Max(1, info.MemberCount),
                        CreatorAccountId = accountId,
                        CreatedAt = info.CreatedAt ?? now,
                        SystemCreatedAtUtc = now,
                        SyncedAt = now
                    });

                    await groupManagement.UpsertAccountGroupAsync(accountId, saved.Id, true, true, now);

                    // 邀请客户
                    var invited = new List<Customer>();
                    var inviteErrors = new List<string>();
                    var accountIsHealthy = true;

                    foreach (var customer in chunk)
                    {
                        if (!accountIsHealthy)
                        {
                            logger.LogWarning("账号 {AccountId} 已失效，停止邀请当前群的剩余客户", accountId);
                            break;
                        }

                        var target = !string.IsNullOrWhiteSpace(customer.Username)
                            ? "@" + customer.Username
                            : customer.Phone;

                        if (string.IsNullOrWhiteSpace(target))
                        {
                            lock (progressLock) { failed++; }
                            inviteErrors.Add($"客户 #{customer.Id} 缺少手机号和用户名");
                            logger.LogWarning("客户 {CustomerId} 缺少手机号和用户名", customer.Id);
                            continue;
                        }

                        var result = await InviteWithRetryAsync(groupService, accountId, info.TelegramId, target, config, ct, logger);

                        if (result.Success)
                        {
                            invited.Add(customer);
                            logger.LogInformation("成功邀请客户 {CustomerId} ({Target})", customer.Id, target);
                        }
                        else
                        {
                            lock (progressLock) { failed++; }
                            inviteErrors.Add($"{target}: {result.Error}");
                            logger.LogWarning("邀请客户 {CustomerId} ({Target}) 失败: {Error}",
                                customer.Id, target, result.Error);

                            // 检查是否是账号级别的失败
                            if (IsAccountFailure(result.Error))
                            {
                                accountIsHealthy = false;
                                logger.LogError("执行账号 {AccountId} 标记为失效: {Error}", accountId, result.Error);
                                // 不将账号放回健康池
                                break;
                            }
                        }

                        await Delay(config, ct);
                    }

                    // 账号仍然健康，放回池中
                    if (accountIsHealthy)
                    {
                        healthyAccounts.Add(accountId);
                    }

                    // 检查成功邀请数量
                    if (invited.Count < Math.Max(1, config.MinSuccessfulInvites))
                    {
                        logger.LogWarning("群组 {TelegramId} 成功邀请人数 {Invited} 不足最小要求 {Min}，跳过活跃消息发送",
                            info.TelegramId, invited.Count, config.MinSuccessfulInvites);
                        await UpdateProgress(host, config, db, taskManagement, completed, failed, ct);
                        return;
                    }

                    logger.LogInformation("群组 {TelegramId} 成功邀请 {Count} 人，开始发送活跃消息",
                        info.TelegramId, invited.Count);

                    // 发送活跃消息
                    var resolved = await tools.ResolveChatTargetAsync(accountId, info.TelegramId.ToString(), ct);
                    if (!resolved.Success || resolved.Target == null)
                    {
                        logger.LogError("无法解析新建群组 {TelegramId}: {Error}", info.TelegramId, resolved.Error);
                        throw new InvalidOperationException(resolved.Error ?? "无法解析新建群组");
                    }

                    foreach (var message in config.ActivityMessages.Where(x => !string.IsNullOrWhiteSpace(x)))
                    {
                        var sent = await tools.SendMessageToResolvedChatAsync(accountId, resolved.Target, message, cancellationToken: ct);
                        if (!sent.Success)
                        {
                            logger.LogError("发送活跃消息失败: {Error}", sent.Error);
                            throw new InvalidOperationException(sent.Error ?? "活跃消息发送失败");
                        }
                        logger.LogInformation("成功发送活跃消息: {Message}", message.Substring(0, Math.Min(50, message.Length)));
                        await Delay(config, ct);
                    }

                    // 标记客户为已沟通
                    lock (progressLock)
                    {
                        foreach (var customer in invited)
                        {
                            customer.InteractionStatus = "contacted";
                            customer.LastInteractionAt ??= DateTime.UtcNow;
                            customer.UpdatedAt = DateTime.UtcNow;

                            if (config.CompletedCustomerIds.Add(customer.Id))
                            {
                                completed++;
                            }
                        }
                    }

                    await db.SaveChangesAsync(ct);
                    await UpdateProgress(host, config, db, taskManagement, completed, failed, ct);

                    logger.LogInformation("完成第 {Seq} 个群组，已完成 {Completed}，失败 {Failed}",
                        groupSeq, completed, failed);
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    logger.LogError(ex, "创建群组或邀请过程中发生异常");

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

        if (healthyAccounts.IsEmpty)
        {
            logger.LogError("所有执行账号均已失效");
            throw new InvalidOperationException("所有执行账号均已失效");
        }
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
        public List<int> AccountIds { get; set; } = [];
        public int? AccountCategoryId { get; set; }
        public List<int> CustomerGroupIds { get; set; } = [];
        public int CustomersPerGroup { get; set; } = 10;
        public string AssignmentMode { get; set; } = "queue";
        public int WorkerCount { get; set; } = 1;
        public string? GroupTitleTemplate { get; set; }
        public string? GroupAboutTemplate { get; set; }
        public List<string> ActivityMessages { get; set; } = [];
        public int MinDelaySeconds { get; set; } = 3;
        public int MaxDelaySeconds { get; set; } = 8;
        public int MinSuccessfulInvites { get; set; } = 1;
        public HashSet<int> CompletedCustomerIds { get; set; } = [];
    }
}
