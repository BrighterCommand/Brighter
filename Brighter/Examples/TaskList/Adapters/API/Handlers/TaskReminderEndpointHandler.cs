using System;
using System.Net;
using OpenRasta.Web;
using paramore.brighter.commandprocessor;
using Tasklist.Adapters.API.Resources;
using Tasks.Ports.Commands;

namespace Tasklist.Adapters.API.Handlers
{
    public class TaskReminderEndpointHandler
    {
        private readonly IAmACommandProcessor _commandProcessor;

        public TaskReminderEndpointHandler(IAmACommandProcessor commandProcessor)
        {
            _commandProcessor = commandProcessor;
        }

        [HttpOperation(HttpMethod.POST)]
        public OperationResult Post(TaskReminderModel reminder)
        {
            var reminderCommand = new TaskReminderCommand(
                taskName: reminder.TaskName,
                dueDate: DateTime.Parse(reminder.DueDate),
                recipient: reminder.Recipient,
                copyTo: reminder.CopyTo
                );

            _commandProcessor.Post(reminderCommand);

            return new OperationResult.OK()
            {
                StatusCode = (int)HttpStatusCode.Accepted
            };
        }
    }
}