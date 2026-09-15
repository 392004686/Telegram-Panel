namespace TelegramPanel.Data.Entities;

public class CustomerImportBatch
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? SourceName { get; set; }
    public int Total { get; set; }
    public int Imported { get; set; }
    public int Duplicates { get; set; }
    public int Invalid { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<CustomerBatchItem> Items { get; set; } = new List<CustomerBatchItem>();
}

public class CustomerBatchItem
{
    public int CustomerImportBatchId { get; set; }
    public CustomerImportBatch CustomerImportBatch { get; set; } = null!;
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public string RawValue { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
