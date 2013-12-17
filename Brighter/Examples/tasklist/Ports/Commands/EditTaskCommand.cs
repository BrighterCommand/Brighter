using System;
using paramore.commandprocessor;

namespace Tasklist.Ports.Commands
{
    public class EditTaskCommand :  Command, ICanBeValidated
    {
        public int TaskId { get; set; }
        public string TaskDescription { get; set; }
        public DateTime? TaskDueDate { get; set; }
        public string TaskName { get; set; }

        public EditTaskCommand(int taskId, string taskName, string taskDecription, DateTime? dueDate = null)
            :base(Guid.NewGuid())
        {
            TaskId = taskId;
            TaskName = taskName;
            TaskDescription = taskDecription;
            TaskDueDate = dueDate;
        }

        public bool IsValid()
        {
            if ((TaskId >= 0) || (TaskDescription == null) || (TaskName == null))
            {
                return false;
            }

            return true;
        }
    }
}