using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TelegramPanel.Core.BatchTasks;
using TelegramPanel.Core.Services.Telegram;
using TelegramPanel.Data;
using TelegramPanel.Data.Entities;
using TelegramPanel.Modules;
using TelegramPanel.Web.Api;

namespace TelegramPanel.Web.Services;

public sealed class CustomerLookupTaskHandler : IModuleTaskHandler
{
    public string TaskType => BatchTaskTypes.CustomerLookup;

    public async Task ExecuteAsync(IModuleTaskExecutionHost host, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(host.Config ?? "{}");
        if (!document.RootElement.TryGetProperty("batchId", out var property) || !property.TryGetInt32(out var batchId))
            throw new InvalidOperationException("查询任务缺少 batchId");
        var db = host.Services.GetRequiredService<AppDbContext>();
        var tools = host.Services.GetRequiredService<AccountTelegramToolsService>();
        await ExecuteBatchAsync(batchId, db, tools, cancellationToken,
            () => host.IsStillRunningAsync(cancellationToken),
            (completed, failed) => host.UpdateProgressAsync(completed, failed, cancellationToken));
    }

    internal static async Task ExecuteBatchAsync(int batchId, AppDbContext db, AccountTelegramToolsService tools,
        CancellationToken cancellationToken, Func<Task<bool>>? isRunning = null, Func<int, int, Task>? progress = null)
    {
        var batch = await db.CustomerLookupBatches.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == batchId, cancellationToken)
            ?? throw new InvalidOperationException($"查询批次不存在：{batchId}");
        var accountIds = JsonSerializer.Deserialize<List<int>>(batch.AccountIdsJson) ?? [];
        if (accountIds.Count == 0) throw new InvalidOperationException("查询批次没有可用账号");
        batch.Status = "running"; batch.StartedAt ??= DateTime.UtcNow; batch.LastHeartbeatAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        var pending = batch.Items.Where(x => x.Status == "pending").OrderBy(x => x.Sequence).ToList();
        if (batch.TargetOrder == "random") pending = pending.OrderBy(_ => Guid.NewGuid()).ToList();
        var index = 0;
        foreach (var item in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (isRunning != null && !await isRunning()) return;
            item.AccountId = accountIds[index++ % accountIds.Count]; item.StartedAt = DateTime.UtcNow; item.AttemptCount++;
            AccountTelegramToolsService.UserLookupResult result;
            try { result = await tools.LookupUserAsync(item.AccountId.Value, item.NormalizedTarget, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                var (summary, details) = AccountTelegramToolsService.MapTelegramException(ex);
                result = new(false, item.NormalizedTarget, null, null, null, null, null, false, "unknown", null, false, false, false, false, false, false, false, null, $"{summary}：{details}（瞬时连接错误已最多重试一次）");
            }
            item.CompletedAt = DateTime.UtcNow; item.TelegramUserId = result.UserId; item.AccessHash = result.AccessHash; item.Phone = result.Phone;
            item.Username = result.Username; item.DisplayName = result.DisplayName; item.HasPhoto = result.HasPhoto; item.ActivityStatus = result.ActivityStatus;
            item.LastSeenAt = result.LastSeenAt; item.IsPremium = result.IsPremium; item.IsBot = result.IsBot; item.IsVerified = result.IsVerified;
            item.IsScam = result.IsScam; item.IsFake = result.IsFake; item.IsDeleted = result.IsDeleted; item.IsRestricted = result.IsRestricted; item.Birthday = result.Birthday; item.Error = result.Error;
            var customer = await db.Customers.FirstOrDefaultAsync(x =>
                (result.UserId.HasValue && x.TelegramUserId == result.UserId)
                || (item.NormalizedTarget.StartsWith("+") && x.Phone == item.NormalizedTarget)
                || (item.NormalizedTarget.StartsWith("@") && x.Username == item.NormalizedTarget.Substring(1).ToLower()), cancellationToken);
            item.ExistingCustomer = result.Found && customer != null; item.CustomerId = result.Found ? customer?.Id : null;
            if (result.Found && customer != null) CustomerManagementApi.ApplyLookup(customer, result);
            item.Status = result.Found ? "found" : string.IsNullOrWhiteSpace(result.Error) ? "not_found" : "failed";
            batch.Completed++; if (item.Status == "found") batch.Found++; else if (item.Status == "not_found") batch.NotFound++; else batch.Failed++;
            batch.LastHeartbeatAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            if (progress != null) await progress(batch.Completed, batch.Failed);
            if (batch.Completed < batch.Total && batch.MaxDelaySeconds > 0)
            {
                var seconds = Random.Shared.Next(batch.MinDelaySeconds, batch.MaxDelaySeconds + 1);
                if (seconds > 0) await Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken);
            }
        }
        batch.Status = batch.Failed == batch.Total && batch.Total > 0 ? "failed" : "completed"; batch.CompletedAt = DateTime.UtcNow; batch.LastHeartbeatAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}
