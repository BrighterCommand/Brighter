using System;
using Paramore.Brighter;

namespace Tasks.Ports.Events
{
    public class TaskReminderSentEvent : Event
    {
        public int TaskId { get; private set; }
        public string TaskName { get; private set; }
        public DateTime DueDate { get; private set; }
        public string ReminderTo { get; private set; }
        public string CopyReminderTo { get; private set; }

        public TaskReminderSentEvent(Guid id, int taskid, string taskName, DateTime dueDate, string reminderTo, string copyReminderTo)
            : base(id)
        {
            this.TaskId = taskid;
            this.TaskName = taskName;
            this.DueDate = dueDate;
            this.ReminderTo = reminderTo;
            this.CopyReminderTo = copyReminderTo;
        }
    }
}