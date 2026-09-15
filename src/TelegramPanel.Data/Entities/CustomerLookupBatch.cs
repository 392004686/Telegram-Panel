namespace TelegramPanel.Data.Entities;

public class CustomerLookupBatch
{
    public int Id { get; set; }
    public int? BatchTaskId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Mode { get; set; } = "task";
    public string AccountSource { get; set; } = "accounts";
    public string AccountIdsJson { get; set; } = "[]";
    public int? AccountCategoryId { get; set; }
    public string TargetOrder { get; set; } = "queue";
    public int MinDelaySeconds { get; set; }
    public int MaxDelaySeconds { get; set; }
    public string Status { get; set; } = "pending";
    public int Total { get; set; }
    public int Completed { get; set; }
    public int Found { get; set; }
    public int NotFound { get; set; }
    public int Failed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? LastHeartbeatAt { get; set; }
    public ICollection<CustomerLookupItem> Items { get; set; } = new List<CustomerLookupItem>();
}

public class CustomerLookupItem
{
    public int Id { get; set; }
    public int CustomerLookupBatchId { get; set; }
    public CustomerLookupBatch Batch { get; set; } = null!;
    public string RawTarget { get; set; } = string.Empty;
    public string NormalizedTarget { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public int? AccountId { get; set; }
    public int? CustomerId { get; set; }
    public bool ExistingCustomer { get; set; }
    public string Status { get; set; } = "pending";
    public string? Error { get; set; }
    public int AttemptCount { get; set; }
    public long? TelegramUserId { get; set; }
    public long? AccessHash { get; set; }
    public string? Phone { get; set; }
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public bool HasPhoto { get; set; }
    public string ActivityStatus { get; set; } = "unknown";
    public DateTime? LastSeenAt { get; set; }
    public bool IsPremium { get; set; }
    public bool IsBot { get; set; }
    public bool IsVerified { get; set; }
    public bool IsScam { get; set; }
    public bool IsFake { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsRestricted { get; set; }
    public string? Birthday { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
