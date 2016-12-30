using System;
using paramore.brighter.commandprocessor;

namespace Tasks.Ports.Events
{
    public class TaskAddedEvent : Event
    {
        public int TaskId { get; private set; }
        public string TaskName { get; private set; }
        public string TaskDecription { get; private set; }
        public DateTime? DueDate { get; private set; }

        public TaskAddedEvent(Guid id, int taskId, string taskName, string taskDecription, DateTime? dueDate = null)
            : base(id)
        {
            this.TaskId = taskId;
            this.TaskName = taskName;
            this.TaskDecription = taskDecription;
            this.DueDate = dueDate;
        }
    }
}