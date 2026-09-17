namespace TelegramPanel.Data.Entities;

public class BatchTaskLog
{
    public long Id { get; set; }
    public int BatchTaskId { get; set; }
    public string Level { get; set; } = "info";
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
