using Paramore.Brighter;

namespace TaskStatus.Driving_Ports;

public class TaskUpdated(string id, App.TaskStatus status,  DateTimeOffset dueAt, IEnumerable<DateTimeOffset> reminders) : Event(id)
{
        public App.TaskStatus Status { get; } = status;
        public DateTimeOffset DueAt { get; } = dueAt;
        public IEnumerable<DateTimeOffset> Reminders { get; } = reminders;
}
