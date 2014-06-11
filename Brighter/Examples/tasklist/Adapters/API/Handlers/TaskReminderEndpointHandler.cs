using System;
using OpenRasta.Web;
using Tasklist.Adapters.API.Resources;
using Tasks.Ports.Commands;

namespace Tasklist.Adapters.API.Handlers
{
    public class TaskReminderEndpointHandler
    {
        [HttpOperation(HttpMethod.POST)]
        public OperationResult Post(TaskReminderModel reminder)
        {
          /*
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
           */

            var reminderCommand = new TaskReminderCommand(
                
                );
        }
    }
}