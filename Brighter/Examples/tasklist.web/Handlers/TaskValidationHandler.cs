using System;
using paramore.commandprocessor;

namespace tasklist.web.Handlers
{
    public class TaskValidationHandler : RequestHandler<Commands.AddTaskCommand>
    {
        public override Commands.AddTaskCommand Handle(Commands.AddTaskCommand command)
        {
            if ((command.TaskDecription == null) || (command.TaskName == null))
            {
                throw new Exception("A valid task needs both a description and a name");
            }

            return base.Handle(command);
        }
    }
}