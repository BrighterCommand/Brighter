using Tasklist.Adapters.DataAccess;
using Tasklist.Domain;
using Tasklist.Ports.Commands;
using paramore.commandprocessor;

namespace Tasklist.Ports.Handlers
{
    public class EditTaskCommandHandler : RequestHandler<EditTaskCommand>
    {
        private readonly ITasksDAO tasksDAO;

        public EditTaskCommandHandler(ITasksDAO tasksDAO)
        {
            this.tasksDAO = tasksDAO;
        }

        [Validation(step: 2, timing: HandlerTiming.Before)]
        [Trace(step:1, timing: HandlerTiming.Before)]
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