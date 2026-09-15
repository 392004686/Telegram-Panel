namespace TelegramPanel.Data.Entities;

public class Customer
{
    public int Id { get; set; }
    public string? Phone { get; set; }
    public string? Username { get; set; }
    public long? TelegramUserId { get; set; }
    public long? AccessHash { get; set; }
    public string? DisplayName { get; set; }
    public string LookupStatus { get; set; } = "pending";
    public string InteractionStatus { get; set; } = "uncontacted";
    public string? Remark { get; set; }
    public DateTime? LastLookupAt { get; set; }
    public DateTime? LastInteractionAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<CustomerGroupAssignment> GroupAssignments { get; set; } = new List<CustomerGroupAssignment>();
    public ICollection<CustomerBatchItem> BatchItems { get; set; } = new List<CustomerBatchItem>();
}
