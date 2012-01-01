using paramore.commandprocessor;
using tasklist.web.Commands;
using tasklist.web.DataAccess;
using tasklist.web.Models;

namespace tasklist.web.Handlers
{
    public class AddTaskCommandHandler : RequestHandler<AddTaskCommand>
    {
        private readonly ITasksDAO tasksDao;

        public AddTaskCommandHandler(ITasksDAO tasksDao)
        {
            this.tasksDao = tasksDao;
        }

        public override AddTaskCommand Handle(AddTaskCommand askTaskCommand)
        {
            tasksDao.Add(
                new Task(
                    taskName: askTaskCommand.TaskName, 
                    taskDecription: askTaskCommand.TaskDecription
                    )
                );

            return askTaskCommand;
        }
    }
}