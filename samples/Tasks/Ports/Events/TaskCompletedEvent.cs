using System;
using Paramore.Brighter;

namespace Tasks.Ports.Events
{
    public class TaskCompletedEvent : Event
    {
        public int TaskId { get; private set; }
        public DateTime CompletionDate { get; private set; }

        public TaskCompletedEvent(Guid id, int taskId, DateTime completionDate)
            : base(id)
        {
            this.TaskId = taskId;
            this.CompletionDate = completionDate;
        }
    }
}