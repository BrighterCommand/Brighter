using System;
using paramore.brighter.commandprocessor;

namespace Tasks.Ports.Commands
{
   public class TaskReminderCommand : Command
    {
       public TaskReminderCommand() : base(Guid.Empty) {}

       public TaskReminderCommand(string taskName, DateTime dueDate, string recipient, string copyTo)
           : base(Guid.NewGuid())
       {
           TaskName = taskName;
           DueDate = dueDate;
           Recipient = recipient;
           CopyTo = copyTo;
       }

        public string TaskName { get; set; }
        public DateTime DueDate { get; set; }
        public string Recipient { get; set; }
        public string CopyTo { get; set; }
    }
}