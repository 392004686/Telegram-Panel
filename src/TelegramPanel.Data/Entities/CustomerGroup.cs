namespace TelegramPanel.Data.Entities;

public class CustomerGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<CustomerGroupAssignment> Assignments { get; set; } = new List<CustomerGroupAssignment>();
}

public class CustomerGroupAssignment
{
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int CustomerGroupId { get; set; }
    public CustomerGroup CustomerGroup { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
