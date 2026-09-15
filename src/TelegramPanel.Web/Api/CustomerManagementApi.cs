using Microsoft.EntityFrameworkCore;
using TelegramPanel.Core.Services.Telegram;
using TelegramPanel.Data;
using TelegramPanel.Data.Entities;

namespace TelegramPanel.Web.Api;

public static class CustomerManagementApi
{
    public static void MapCustomerManagementApi(this RouteGroupBuilder api)
    {
        api.MapGet("/customers", ListAsync);
        api.MapGet("/customers/{id:int}", DetailAsync);
        api.MapPost("/customers/import", ImportAsync);
        api.MapPost("/customers/{id:int}/lookup", LookupAsync);
        api.MapPost("/customers/lookup", LookupNewAsync);
        api.MapPost("/customers/batch", BatchAsync);
        api.MapDelete("/customers/{id:int}", DeleteAsync);
        api.MapGet("/customer-groups", GroupsAsync);
        api.MapPost("/customer-groups", CreateGroupAsync);
        api.MapPut("/customer-groups/{id:int}", UpdateGroupAsync);
        api.MapDelete("/customer-groups/{id:int}", DeleteGroupAsync);
        api.MapGet("/customer-import-batches", BatchesAsync);
    }

    private static async Task<IResult> ListAsync(int page, int pageSize, string? search, string? status, int? groupId, int? batchId, AppDbContext db)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
        var query = db.Customers.AsNoTracking().Include(x => x.GroupAssignments).ThenInclude(x => x.CustomerGroup).Include(x => x.BatchItems).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => (x.Phone != null && x.Phone.Contains(search)) || (x.Username != null && x.Username.Contains(search)) || (x.DisplayName != null && x.DisplayName.Contains(search)) || (x.TelegramUserId != null && x.TelegramUserId.ToString()!.Contains(search)));
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.LookupStatus == status);
        if (groupId.HasValue) query = query.Where(x => x.GroupAssignments.Any(g => g.CustomerGroupId == groupId));
        if (batchId.HasValue) query = query.Where(x => x.BatchItems.Any(b => b.CustomerImportBatchId == batchId));
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).Select(x => new CustomerDto(x.Id, x.Phone, x.Username, x.TelegramUserId, x.DisplayName, x.HasPhoto, x.ActivityStatus, x.LastSeenAt, x.IsPremium, x.IsBot, x.IsVerified, x.IsScam, x.IsFake, x.IsDeleted, x.Birthday, x.LookupStatus, x.InteractionStatus, x.Remark, x.LastLookupAt, x.CreatedAt, x.GroupAssignments.Select(g => new NamedDto(g.CustomerGroupId, g.CustomerGroup.Name)).ToList(), x.BatchItems.Select(b => b.CustomerImportBatchId).ToList())).ToListAsync();
        return Results.Ok(new { items, total, page, pageSize });
    }

    private static async Task<IResult> ImportAsync(CustomerImportRequest request, AppDbContext db)
    {
        var rawItems = (request.Values ?? "").Split(new[] { '\r', '\n', ',', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (rawItems.Length == 0) return Results.BadRequest(new { success = false, message = "请填写手机号或 @用户名" });
        CustomerGroup? group = null;
        if (request.GroupId.HasValue) group = await db.CustomerGroups.FindAsync(request.GroupId.Value);
        if (group == null && !string.IsNullOrWhiteSpace(request.NewGroupName))
        {
            var name = request.NewGroupName.Trim();
            group = await db.CustomerGroups.FirstOrDefaultAsync(x => x.Name == name) ?? new CustomerGroup { Name = name };
            if (group.Id == 0) db.CustomerGroups.Add(group);
        }
        var batch = new CustomerImportBatch { Name = string.IsNullOrWhiteSpace(request.BatchName) ? $"导入批次 {DateTime.Now:yyyyMMdd-HHmm}" : request.BatchName.Trim(), SourceName = request.SourceName, Total = rawItems.Length };
        db.CustomerImportBatches.Add(batch);
        await db.SaveChangesAsync();
        var imported = 0; var duplicates = 0; var invalid = 0;
        foreach (var raw in rawItems)
        {
            var value = raw.Trim();
            if (!TryNormalizeTarget(value, out var phone, out var username)) { invalid++; continue; }
            var customer = await db.Customers.FirstOrDefaultAsync(x => (phone != null && x.Phone == phone) || (username != null && x.Username == username));
            if (customer == null)
            {
                customer = new Customer { Phone = phone, Username = username };
                db.Customers.Add(customer); imported++;
            }
            else duplicates++;
            await db.SaveChangesAsync();
            if (!await db.CustomerBatchItems.AnyAsync(x => x.CustomerImportBatchId == batch.Id && x.CustomerId == customer.Id)) db.CustomerBatchItems.Add(new CustomerBatchItem { CustomerImportBatchId = batch.Id, CustomerId = customer.Id, RawValue = raw });
            if (group != null && !await db.CustomerGroupAssignments.AnyAsync(x => x.CustomerId == customer.Id && x.CustomerGroupId == group.Id)) db.CustomerGroupAssignments.Add(new CustomerGroupAssignment { CustomerId = customer.Id, CustomerGroupId = group.Id });
        }
        batch.Imported = imported; batch.Duplicates = duplicates; batch.Invalid = invalid;
        await db.SaveChangesAsync();
        return Results.Ok(new { success = true, batchId = batch.Id, total = rawItems.Length, imported, duplicates, invalid });
    }

    private static async Task<IResult> DeleteAsync(int id, AppDbContext db) { var item = await db.Customers.FindAsync(id); if (item == null) return Results.NotFound(); db.Customers.Remove(item); await db.SaveChangesAsync(); return Results.Ok(new { success = true }); }
    private static async Task<IResult> DetailAsync(int id, AppDbContext db)
    {
        var item = await db.Customers.AsNoTracking().Include(x => x.GroupAssignments).ThenInclude(x => x.CustomerGroup).Include(x => x.BatchItems).ThenInclude(x => x.CustomerImportBatch).FirstOrDefaultAsync(x => x.Id == id);
        return item == null ? Results.NotFound() : Results.Ok(new CustomerDetailDto(item.Id, item.Phone, item.Username, item.TelegramUserId, item.AccessHash, item.DisplayName, item.HasPhoto, item.ActivityStatus, item.LastSeenAt, item.IsPremium, item.IsBot, item.IsVerified, item.IsScam, item.IsFake, item.IsDeleted, item.IsRestricted, item.Birthday, item.LookupStatus, item.InteractionStatus, item.Remark, item.LastLookupAt, item.LastInteractionAt, item.CreatedAt, item.UpdatedAt, item.GroupAssignments.Select(x => new NamedDto(x.CustomerGroupId, x.CustomerGroup.Name)).ToList(), item.BatchItems.Select(x => new NamedDto(x.CustomerImportBatchId, x.CustomerImportBatch.Name)).ToList()));
    }
    private static async Task<IResult> LookupAsync(int id, CustomerLookupRequest request, AppDbContext db, AccountTelegramToolsService tools, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.FindAsync([id], cancellationToken);
        if (customer == null) return Results.NotFound();
        var query = customer.Phone ?? (customer.Username == null ? string.Empty : $"@{customer.Username}");
        var result = await tools.LookupUserAsync(request.AccountId, query, cancellationToken);
        ApplyLookup(customer, result);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(result);
    }
    private static async Task<IResult> LookupNewAsync(CustomerDirectLookupRequest request, AppDbContext db, AccountTelegramToolsService tools, CancellationToken cancellationToken)
    {
        var query = (request.Query ?? string.Empty).Trim();
        if (!TryNormalizeTarget(query, out var normalizedPhone, out var normalizedUsername)) return Results.BadRequest(new { message = "仅支持手机号（数字、空格、可选开头 +）或 @用户名" });
        var customer = await db.Customers.FirstOrDefaultAsync(x => (normalizedPhone != null && x.Phone == normalizedPhone) || (normalizedUsername != null && x.Username == normalizedUsername), cancellationToken);
        var result = await tools.LookupUserAsync(request.AccountId, normalizedPhone ?? $"@{normalizedUsername}", cancellationToken);
        if (customer != null) { ApplyLookup(customer, result); await db.SaveChangesAsync(cancellationToken); }
        return Results.Ok(new { customerId = customer?.Id, existingCustomer = customer != null, result });
    }

    private static void ApplyLookup(Customer customer, AccountTelegramToolsService.UserLookupResult result)
    {
        customer.LookupStatus = result.Found ? "found" : string.IsNullOrWhiteSpace(result.Error) ? "not_found" : "error";
        customer.LastLookupAt = DateTime.UtcNow; customer.UpdatedAt = DateTime.UtcNow;
        if (!result.Found) return;
        customer.TelegramUserId = result.UserId; customer.AccessHash = result.AccessHash;
        customer.Phone = string.IsNullOrWhiteSpace(result.Phone) ? customer.Phone : result.Phone;
        customer.Username = string.IsNullOrWhiteSpace(result.Username) ? customer.Username : result.Username;
        customer.DisplayName = result.DisplayName; customer.HasPhoto = result.HasPhoto; customer.ActivityStatus = result.ActivityStatus;
        customer.LastSeenAt = result.LastSeenAt; customer.IsPremium = result.IsPremium; customer.IsBot = result.IsBot;
        customer.IsVerified = result.IsVerified; customer.IsScam = result.IsScam; customer.IsFake = result.IsFake;
        customer.IsDeleted = result.IsDeleted; customer.IsRestricted = result.IsRestricted; customer.Birthday = result.Birthday;
    }

    private static bool TryNormalizeTarget(string value, out string? phone, out string? username)
    {
        phone = null; username = null; value = value.Trim();
        if (value.StartsWith('@'))
        {
            var candidate = value[1..];
            if (candidate.Length is < 3 or > 32 || candidate.Any(c => !char.IsLetterOrDigit(c) && c != '_')) return false;
            username = candidate.ToLowerInvariant(); return true;
        }
        if (value.Length == 0 || value.Any(c => !char.IsDigit(c) && c != ' ' && c != '+') || value.Count(c => c == '+') > 1 || (value.Contains('+') && !value.StartsWith('+'))) return false;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length is < 7 or > 15) return false;
        phone = "+" + digits; return true;
    }
    private static async Task<IResult> BatchAsync(CustomerBatchRequest request, AppDbContext db, CancellationToken cancellationToken)
    {
        var ids = request.Ids.Distinct().ToArray();
        if (ids.Length == 0) return Results.BadRequest(new { message = "请选择客户" });
        var customers = await db.Customers.Include(x => x.GroupAssignments).Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);
        if (request.Action == "delete") db.Customers.RemoveRange(customers);
        else if (request.Action == "set_group")
        {
            var group = request.GroupId.HasValue ? await db.CustomerGroups.FindAsync([request.GroupId.Value], cancellationToken) : null;
            if (request.GroupId.HasValue && group == null) return Results.BadRequest(new { message = "客户分类不存在" });
            foreach (var customer in customers)
            {
                db.CustomerGroupAssignments.RemoveRange(customer.GroupAssignments);
                if (group != null) db.CustomerGroupAssignments.Add(new CustomerGroupAssignment { CustomerId = customer.Id, CustomerGroupId = group.Id });
            }
        }
        else return Results.BadRequest(new { message = "不支持的批量操作" });
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { success = true, affected = customers.Count });
    }
    private static async Task<IResult> GroupsAsync(AppDbContext db) => Results.Ok(await db.CustomerGroups.AsNoTracking().OrderBy(x => x.Name).Select(x => new CustomerGroupDto(x.Id, x.Name, x.Description, x.Assignments.Count)).ToListAsync());
    private static async Task<IResult> CreateGroupAsync(CreateCustomerGroupRequest request, AppDbContext db) { var name = request.Name.Trim(); if (name.Length == 0) return Results.BadRequest(); var item = new CustomerGroup { Name = name, Description = request.Description }; db.CustomerGroups.Add(item); await db.SaveChangesAsync(); return Results.Ok(new NamedDto(item.Id, item.Name)); }
    private static async Task<IResult> UpdateGroupAsync(int id, CreateCustomerGroupRequest request, AppDbContext db) { var item = await db.CustomerGroups.FindAsync(id); if (item == null) return Results.NotFound(); item.Name = request.Name.Trim(); item.Description = request.Description; await db.SaveChangesAsync(); return Results.Ok(new NamedDto(item.Id, item.Name)); }
    private static async Task<IResult> DeleteGroupAsync(int id, AppDbContext db) { var item = await db.CustomerGroups.FindAsync(id); if (item == null) return Results.NotFound(); db.CustomerGroups.Remove(item); await db.SaveChangesAsync(); return Results.Ok(new { success = true }); }
    private static async Task<IResult> BatchesAsync(AppDbContext db) => Results.Ok(await db.CustomerImportBatches.AsNoTracking().OrderByDescending(x => x.Id).Select(x => new CustomerBatchDto(x.Id, x.Name, x.Total, x.Imported, x.Duplicates, x.Invalid, x.CreatedAt)).ToListAsync());

    public sealed record CustomerImportRequest(string Values, string? BatchName, int? GroupId, string? NewGroupName, string? SourceName);
    public sealed record CreateCustomerGroupRequest(string Name, string? Description);
    public sealed record CustomerLookupRequest(int AccountId);
    public sealed record CustomerDirectLookupRequest(int AccountId, string Query);
    public sealed record CustomerBatchRequest(List<int> Ids, string Action, int? GroupId);
    public sealed record NamedDto(int Id, string Name);
    public sealed record CustomerGroupDto(int Id, string Name, string? Description, int CustomerCount);
    public sealed record CustomerBatchDto(int Id, string Name, int Total, int Imported, int Duplicates, int Invalid, DateTime CreatedAt);
    public sealed record CustomerDto(int Id, string? Phone, string? Username, long? TelegramUserId, string? DisplayName, bool HasPhoto, string ActivityStatus, DateTime? LastSeenAt, bool IsPremium, bool IsBot, bool IsVerified, bool IsScam, bool IsFake, bool IsDeleted, string? Birthday, string LookupStatus, string InteractionStatus, string? Remark, DateTime? LastLookupAt, DateTime CreatedAt, List<NamedDto> Groups, List<int> BatchIds);
    public sealed record CustomerDetailDto(int Id, string? Phone, string? Username, long? TelegramUserId, long? AccessHash, string? DisplayName, bool HasPhoto, string ActivityStatus, DateTime? LastSeenAt, bool IsPremium, bool IsBot, bool IsVerified, bool IsScam, bool IsFake, bool IsDeleted, bool IsRestricted, string? Birthday, string LookupStatus, string InteractionStatus, string? Remark, DateTime? LastLookupAt, DateTime? LastInteractionAt, DateTime CreatedAt, DateTime UpdatedAt, List<NamedDto> Groups, List<NamedDto> Batches);
}

public sealed record UserLookupRequest(string Query);
