#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.policy.Attributes;
using paramore.brighter.commandprocessor.Logging;
using Tasks.Adapters.MailGateway;
using Tasks.Ports.Commands;
using Tasks.Ports.Events;

namespace Tasks.Ports.Handlers
{
    public class MailTaskReminderHandler : RequestHandler<TaskReminderCommand>
    {
        private readonly IAmAMailGateway _mailGateway;
        private readonly IAmACommandProcessor _commandProcessor;

        public MailTaskReminderHandler(IAmAMailGateway mailGateway, IAmACommandProcessor commandProcessor)
        {
            _mailGateway = mailGateway;
            _commandProcessor = commandProcessor;
        }

        [RequestLogging(step: 1, timing: HandlerTiming.Before)]
        [UsePolicy(CommandProcessor.CIRCUITBREAKER, step: 2)]
        [UsePolicy(CommandProcessor.RETRYPOLICY, step: 3)]
        public override TaskReminderCommand Handle(TaskReminderCommand taskReminderCommand)
        {
            //_mailGateway.Send(new TaskReminder(
            //    taskName: new TaskName(taskReminderCommand.TaskName),
            //    dueDate: taskReminderCommand.DueDate,
            //    reminderTo: new EmailAddress(taskReminderCommand.Recipient),
            //    copyReminderTo: new EmailAddress(taskReminderCommand.CopyTo)
            //    ));

            _commandProcessor.Post(new TaskReminderSentEvent(taskReminderCommand.Id, taskReminderCommand.TaskId, taskReminderCommand.TaskName, taskReminderCommand.DueDate, taskReminderCommand.Recipient, taskReminderCommand.CopyTo));

            return base.Handle(taskReminderCommand);
        }
    }
}
