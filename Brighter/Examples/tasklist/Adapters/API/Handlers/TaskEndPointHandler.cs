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
        private readonly ITaskListRetriever taskListRetriever;

        public TaskEndPointHandler(ITaskRetriever taskRetriever, ITaskListRetriever taskListRetriever, IAmACommandProcessor commandProcessor)
        {
            this.taskRetriever = taskRetriever;
            this.taskListRetriever = taskListRetriever;
            this.commandProcessor = commandProcessor;
        }

        [HttpOperation(HttpMethod.GET)]
        public OperationResult Get()
        {
            return new OperationResult.OK {ResponseResource = taskListRetriever.RetrieveTasks()};
        }

        [HttpOperation(HttpMethod.GET)]
        public OperationResult Get(int taskId)
        {
            return new OperationResult.OK {ResponseResource = taskRetriever.Get(taskId)};
        }

        [HttpOperation(HttpMethod.POST)]
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