using System;
using OpenRasta.Web;
using Tasklist.Adapters.API.Resources;
using Tasklist.Ports.Commands;
using Tasklist.Ports.ViewModelRetrievers;
using paramore.brighter.commandprocessor;

namespace Tasklist.Adapters.API.Handlers
{
    public class TaskEndPointHandler
    {
        private readonly ITaskRetriever taskRetriever;
        private readonly IAmACommandProcessor commandProcessor;
        private readonly ICommunicationContext communicationContext;
        private readonly ITaskListRetriever taskListRetriever;

        public TaskEndPointHandler(ITaskRetriever taskRetriever, ITaskListRetriever taskListRetriever, IAmACommandProcessor commandProcessor, ICommunicationContext communicationContext)
        {
            this.taskRetriever = taskRetriever;
            this.taskListRetriever = taskListRetriever;
            this.commandProcessor = commandProcessor;
            this.communicationContext = communicationContext;
        }

        [HttpOperation(HttpMethod.GET)]
        public OperationResult Get()
        {
            TaskListModel responseResource = taskListRetriever.RetrieveTasks();
            return new OperationResult.OK {ResponseResource = responseResource};
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
                    CreatedResourceUrl = new Uri(string.Format("{0}/tasks/{1}", communicationContext.ApplicationBaseUri, addTaskCommand.TaskId))
                };
        }
    }
}