using Tasklist.Adapters.DataAccess;
using Tasklist.Domain;
using Tasklist.Ports.Commands;
using paramore.commandprocessor;
using paramore.commandprocessor.timeoutpolicy.Attributes;

namespace Tasklist.Ports.Handlers
{
    public class EditTaskCommandHandler : RequestHandler<EditTaskCommand>
    {
        private readonly ITasksDAO tasksDAO;

        public EditTaskCommandHandler(ITasksDAO tasksDAO)
        {
            this.tasksDAO = tasksDAO;
        }

        [Trace(step:1, timing: HandlerTiming.Before)]
        [Validation(step: 2, timing: HandlerTiming.Before)]
        [TimeoutPolicy(step: 3, milliseconds: 300)]
        public override EditTaskCommand Handle(EditTaskCommand editTaskCommand)
        {
            Task task = tasksDAO.FindById(editTaskCommand.TaskId);

            task.TaskName = editTaskCommand.TaskName;
            task.TaskDescription = editTaskCommand.TaskDescription;
            task.DueDate = editTaskCommand.TaskDueDate;

            tasksDAO.Update(task);

            return editTaskCommand;
        }
    }
}