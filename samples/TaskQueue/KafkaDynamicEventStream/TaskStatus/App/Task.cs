namespace TaskStatus.App;

public enum TaskStatus
{
    NotStarted,
    InProgress,
    Completed,
    Failed
}

public class Task(string id, DateTimeOffset createdAt, DateTimeOffset dueAt, IEnumerable<DateTimeOffset> reminders)
{
    private string Id { get; set; } = id;
    private DateTimeOffset CreatedAt { get; set; } = createdAt;
    DateTimeOffset? CompletedAt { get; set; }
    private DateTimeOffset DueAt { get; set; } = dueAt;
    IEnumerable<DateTimeOffset> Reminders { get; set; } = reminders;
    TaskStatus Status { get;set; }  = TaskStatus.NotStarted;
}
