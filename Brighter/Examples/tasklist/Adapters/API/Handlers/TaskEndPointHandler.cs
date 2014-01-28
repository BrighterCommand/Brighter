using System;
using OpenRasta.Web;
using Tasklist.Adapters.API.Resources;
using Tasklist.Ports.Commands;
using Tasklist.Ports.ViewModelRetrievers;
using paramore.commandprocessor;

namespace Tasklist.Adapters.API.Handlers
{
    public class TaskEndPointHandler
    {
        private readonly ITaskRetriever taskRetriever;
        private readonly IAmACommandProcessor commandProcessor;

        public TaskEndPointHandler(ITaskRetriever taskRetriever, IAmACommandProcessor commandProcessor)
        {
            this.taskRetriever = taskRetriever;
            this.commandProcessor = commandProcessor;
        }

        public OperationResult Get(int taskId)
        {
            return new OperationResult.OK {ResponseResource = taskRetriever.Get(taskId)};
        }

        public OperationResult Post(TaskModel newTask)
        {
            var addTaskCommand = new AddTaskCommand(
                taskName: newTask.TaskName,
                taskDecription: newTask.TaskDescription,
                dueDate: DateTime.Parse(newTask.DueDate)
                );

            commandProcessor.Send(addTaskCommand);

            return new OperationResult.Created
                {
                    ResponseResource = taskRetriever.Get(addTaskCommand.TaskId),
                    CreatedResourceUrl = new Uri(string.Format("http://localhost:49743/tasks/{0}", addTaskCommand.TaskId))
                };
        }
    }
}