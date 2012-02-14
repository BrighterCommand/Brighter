using System;
using paramore.commandprocessor;

namespace tasklist.web.Commands
{
    public class AddTaskCommand : Command, ICanBeValidated
    {
        public string TaskName { get; set; }
        public string TaskDecription { get; set; }

        public AddTaskCommand(string taskName, string taskDecription)
            :base(Guid.NewGuid())
        {
            TaskName = taskName;
            TaskDecription = taskDecription;
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