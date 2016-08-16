using System;
using Microsoft.AspNetCore.Mvc;
using paramore.brighter.commandprocessor;
using Tasks.Ports.Commands;
using TasksApi.Resources;

namespace TasksApi.Controllers
{
    [Route("api/[controller]")]
    public class RemindersController
    {
        private readonly IAmACommandProcessor _commandProcessor;

        public RemindersController(IAmACommandProcessor commandProcessor)
        {
            _commandProcessor = commandProcessor;
        }

        [HttpPost]
        public IActionResult Post([FromBody]TaskReminderModel reminder)
        {
            var reminderCommand = new TaskReminderCommand(
                taskId: reminder.TaskId,
                taskName: reminder.TaskName,
                dueDate: DateTime.Parse(reminder.DueDate),
                recipient: reminder.Recipient,
                copyTo: reminder.CopyTo
            );

            _commandProcessor.Post(reminderCommand);

            return new StatusCodeResult(202) ;
        }

    }
}