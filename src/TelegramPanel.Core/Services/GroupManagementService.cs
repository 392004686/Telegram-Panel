using TelegramPanel.Data.Entities;
using TelegramPanel.Data.Repositories;

namespace TelegramPanel.Core.Services;

/// <summary>
/// 群组数据管理服务
/// </summary>
public class GroupManagementService
{
    private readonly IGroupRepository _groupRepository;
    private readonly IAccountGroupRepository _accountGroupRepository;

    public GroupManagementService(IGroupRepository groupRepository, IAccountGroupRepository accountGroupRepository)
    {
        _groupRepository = groupRepository;
        _accountGroupRepository = accountGroupRepository;
    }

    public async Task<Group?> GetGroupAsync(int id)
    {
        return await _groupRepository.GetByIdAsync(id);
    }

    public async Task<Group?> GetGroupByTelegramIdAsync(long telegramId)
    {
        return await _groupRepository.GetByTelegramIdAsync(telegramId);
    }

    public async Task<IEnumerable<Group>> GetAllGroupsAsync()
    {
        return await _groupRepository.GetAllAsync();
    }

    public async Task<(IReadOnlyList<Group> Items, int TotalCount)> QueryGroupsForViewPagedAsync(
        int accountId,
        int? categoryId,
        string? filterType,
        string? membershipRole,
        string? search,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return await _groupRepository.QueryForViewPagedAsync(accountId, categoryId, filterType, membershipRole, search, pageIndex, pageSize, cancellationToken);
    }

    public async Task<IEnumerable<Group>> GetGroupsByCreatorAsync(int accountId)
    {
        return await _groupRepository.GetByCreatorAccountAsync(accountId);
    }

    public async Task<Group> CreateOrUpdateGroupAsync(Group group)
    {
        if (group.TelegramId <= 0)
            throw new ArgumentException("TelegramId 必须为正数", nameof(group));

        var existing = await _groupRepository.GetByTelegramIdAsync(group.TelegramId);
        if (existing != null)
        {
            existing.Title = group.Title;
            existing.Username = group.Username;
            existing.MemberCount = group.MemberCount;
            existing.About = group.About;
            existing.AccessHash = group.AccessHash;
            if (group.CurrentStatusCheckedAtUtc.HasValue
                && (!existing.CurrentStatusCheckedAtUtc.HasValue || group.CurrentStatusCheckedAtUtc >= existing.CurrentStatusCheckedAtUtc))
            {
                existing.CurrentStatus = string.IsNullOrWhiteSpace(group.CurrentStatus) ? existing.CurrentStatus : group.CurrentStatus;
                existing.CurrentStatusCheckedAtUtc = group.CurrentStatusCheckedAtUtc;
                existing.CurrentStatusAccountId = group.CurrentStatusAccountId;
            }
            existing.PublicLink = GetPublicLink(group.Username);
            if (group.CategoryId.HasValue)
                existing.CategoryId = group.CategoryId;
            if (existing.CreatorAccountId == null && group.CreatorAccountId != null)
                existing.CreatorAccountId = group.CreatorAccountId;
            if (group.CreatedAt.HasValue)
                existing.CreatedAt = group.CreatedAt;
            if (existing.SystemCreatedAtUtc == null && group.SystemCreatedAtUtc != null)
                existing.SystemCreatedAtUtc = group.SystemCreatedAtUtc;
            existing.SyncedAt = DateTime.UtcNow;

            await _groupRepository.UpdateAsync(existing);
            return existing;
        }

        group.SyncedAt = DateTime.UtcNow;
        group.PublicLink = GetPublicLink(group.Username);
        return await _groupRepository.AddAsync(group);
    }

    public async Task UpdateGroupAsync(Group group)
    {
        group.SyncedAt = DateTime.UtcNow;
        await _groupRepository.UpdateAsync(group);
    }

    public async Task UpdateGroupJoinLinksAsync(int groupId, string? publicLink, string? inviteLink)
    {
        var group = await _groupRepository.GetByIdAsync(groupId);
        if (group == null)
            return;

        group.PublicLink = publicLink;
        if (!string.IsNullOrWhiteSpace(inviteLink))
            group.InviteLink = inviteLink;
        await _groupRepository.UpdateAsync(group);
    }

    private static string? GetPublicLink(string? username) =>
        string.IsNullOrWhiteSpace(username) ? null : $"https://t.me/{username.Trim().TrimStart('@')}";

    public async Task DeleteGroupAsync(int id)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group != null)
            await _groupRepository.DeleteAsync(group);
    }

    public async Task UpdateGroupCategoryAsync(int groupId, int? categoryId)
    {
        var group = await _groupRepository.GetByIdAsync(groupId);
        if (group != null)
        {
            group.CategoryId = categoryId;
            group.SyncedAt = DateTime.UtcNow;
            await _groupRepository.UpdateAsync(group);
        }
    }

    /// <summary>
    /// 一次性保存群组分类绑定。scope 内从目标分类取消勾选的群组会移出分类，其他分类不受影响。
    /// </summary>
    public async Task<int> UpdateGroupCategoryAssignmentsAsync(
        IReadOnlyCollection<int> scopeIds,
        IReadOnlyCollection<int> selectedIds,
        int? categoryId,
        CancellationToken cancellationToken = default)
    {
        var selected = selectedIds
            .Where(x => x > 0)
            .ToHashSet();
        return await _groupRepository.UpdateCategoryAssignmentsAsync(
            scopeIds,
            selected,
            categoryId,
            cancellationToken);
    }

    public async Task<int> GetTotalGroupCountAsync()
    {
        return await _groupRepository.CountAsync();
    }

    public async Task<int> GetGroupCountByCreatorAsync(int accountId)
    {
        return await _groupRepository.CountAsync(g => g.CreatorAccountId == accountId);
    }

    public async Task UpsertAccountGroupAsync(int accountId, int groupId, bool isCreator, bool isAdmin, DateTime syncedAtUtc)
    {
        await _accountGroupRepository.UpsertAsync(new AccountGroup
        {
            AccountId = accountId,
            GroupId = groupId,
            IsCreator = isCreator,
            IsAdmin = isAdmin,
            SyncedAt = syncedAtUtc
        });
    }

    public async Task MarkUnseenAccountGroupsAsync(int accountId, IReadOnlyCollection<int> visibleGroupIds)
    {
        var visibleSet = visibleGroupIds.Count == 0
            ? new HashSet<int>()
            : visibleGroupIds.ToHashSet();

        var staleMemberships = (await _accountGroupRepository.GetByAccountAsync(accountId))
            .Where(x => !visibleSet.Contains(x.GroupId))
            .Distinct()
            .ToList();

        var checkedAt = DateTime.UtcNow;
        foreach (var membership in staleMemberships)
        {
            if (membership.Group != null)
            {
                // 保留关联，仅记录该账号最近一次未看到此群的观察结果。
                if (!membership.Group.CurrentStatusCheckedAtUtc.HasValue || checkedAt >= membership.Group.CurrentStatusCheckedAtUtc)
                {
                    membership.Group.CurrentStatus = "账号不可见";
                    membership.Group.CurrentStatusCheckedAtUtc = checkedAt;
                    membership.Group.CurrentStatusAccountId = accountId;
                    await _groupRepository.UpdateAsync(membership.Group);
                }
            }
        }

        foreach (var groupId in staleMemberships.Select(x => x.GroupId).Distinct())
            await RemoveAccountGroupAsync(groupId, accountId);
    }

    public async Task MarkAccountGroupNotVisibleAsync(int groupId, int accountId, DateTime checkedAtUtc)
    {
        var group = await _groupRepository.GetByIdAsync(groupId);
        if (group != null
            && (!group.CurrentStatusCheckedAtUtc.HasValue || checkedAtUtc >= group.CurrentStatusCheckedAtUtc))
        {
            group.CurrentStatus = "账号不可见";
            group.CurrentStatusCheckedAtUtc = checkedAtUtc;
            group.CurrentStatusAccountId = accountId;
            await _groupRepository.UpdateAsync(group);
        }

        // Keep the local group record so the just-refreshed "not visible" state
        // remains inspectable even when this was its last account association.
        await _accountGroupRepository.DeleteAsync(accountId, groupId);
    }

    public async Task<IReadOnlyList<AccountGroup>> GetAccountGroupMembershipsAsync(int accountId, CancellationToken cancellationToken = default)
    {
        return await _accountGroupRepository.GetByAccountAsync(accountId, cancellationToken);
    }

    public async Task<IReadOnlyList<AccountGroup>> GetGroupAccountMembershipsAsync(int groupId, CancellationToken cancellationToken = default)
    {
        return await _accountGroupRepository.GetByGroupAsync(groupId, cancellationToken);
    }

    public async Task RemoveAccountGroupAsync(int groupId, int accountId)
    {
        var group = await _groupRepository.GetByIdAsync(groupId);
        if (group == null)
            return;

        var hasRemainingRelations = group.AccountGroups.Any(x => x.AccountId != accountId);
        var creatorRemoved = group.CreatorAccountId == accountId;

        await _accountGroupRepository.DeleteAsync(accountId, groupId);

        if (creatorRemoved)
            group.CreatorAccountId = null;

        if (!hasRemainingRelations && group.CreatorAccountId == null)
        {
            await _groupRepository.DeleteAsync(group);
            return;
        }

        if (!creatorRemoved)
            return;

        group.SyncedAt = DateTime.UtcNow;
        await _groupRepository.UpdateAsync(group);
    }

    private async Task DetachCreatorFromGroupAsync(int groupId, int accountId)
    {
        var group = await _groupRepository.GetByIdAsync(groupId);
        if (group == null || group.CreatorAccountId != accountId)
            return;

        group.CreatorAccountId = null;

        if (group.AccountGroups.Count == 0)
        {
            await _groupRepository.DeleteAsync(group);
            return;
        }

        group.SyncedAt = DateTime.UtcNow;
        await _groupRepository.UpdateAsync(group);
    }

    /// <summary>
    /// 解析群组操作的执行账号：
    /// 优先使用 preferredAccountId，其次 CreatorAccountId，否则从关联表中挑选一个管理员账号。
    /// </summary>
    public async Task<int?> ResolveExecuteAccountIdAsync(Group group, int? preferredAccountId = null)
    {
        return await ResolveAdminAccountIdAsync(group.Id, preferredAccountId);
    }

    public async Task<int?> ResolveAdminAccountIdAsync(int groupId, int? preferredAccountId = null, CancellationToken cancellationToken = default)
    {
        var memberships = await _accountGroupRepository.GetByGroupAsync(groupId, cancellationToken);
        var eligible = memberships
            .Where(x => x.Account != null && x.Account.IsActive && (x.IsCreator || x.IsAdmin))
            .OrderByDescending(x => preferredAccountId.HasValue && x.AccountId == preferredAccountId.Value)
            .ThenByDescending(x => x.IsCreator)
            .ThenBy(x => x.AccountId)
            .FirstOrDefault();
        return eligible?.AccountId;
    }
}


