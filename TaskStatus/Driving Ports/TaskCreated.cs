using Paramore.Brighter;

namespace TaskStatus.Driving_Ports;

public class TaskCreated(string id, DateTimeOffset createdAt, DateTimeOffset dueAt, IEnumerable<DateTimeOffset> reminders)
    : Event(id)
{
    public DateTimeOffset CreatedAt { get; } = createdAt;
    public DateTimeOffset DueAt { get; } = dueAt;
    public IEnumerable<DateTimeOffset> Reminders { get; } = reminders;
}
