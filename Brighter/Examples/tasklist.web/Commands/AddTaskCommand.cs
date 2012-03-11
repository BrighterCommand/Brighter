using System;
using paramore.commandprocessor;

namespace tasklist.web.Commands
{
    public class AddTaskCommand : Command, ICanBeValidated
    {
        public string TaskDecription { get; set; }
        public DateTime? TaskDueDate { get; set; }
        public string TaskName { get; set; }

        public AddTaskCommand(string taskName, string taskDecription, DateTime? dueDate = null)
            :base(Guid.NewGuid())
        {
            TaskName = taskName;
            TaskDecription = taskDecription;
            TaskDueDate = dueDate;
        }

        public bool IsValid()
        {
            if ((TaskDecription == null) || (TaskName == null))
            {
                return false;
            }

            return true;
        }
    }
}