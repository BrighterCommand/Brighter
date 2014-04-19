using System;
using Common.Logging;
using Tasklist.Adapters.DataAccess;
using Tasklist.Domain;
using Tasklist.Ports.Commands;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.timeoutpolicy.Attributes;

namespace Tasklist.Ports.Handlers
{
    public class CompleteTaskCommandHandler : RequestHandler<CompleteTaskCommand>
    {
        readonly ITasksDAO tasksDAO;

        public CompleteTaskCommandHandler(ITasksDAO tasksDao, ILog logger) : base(logger)
        {
            tasksDAO = tasksDao;
        }

        [RequestLogging(step:1, timing: HandlerTiming.Before)]
        [Validation(step: 2, timing: HandlerTiming.Before)]
        [TimeoutPolicy(step: 3, milliseconds: 300)]
        public override CompleteTaskCommand Handle(CompleteTaskCommand completeTaskCommand)
        {
            Task task = tasksDAO.FindById(completeTaskCommand.TaskId);
            if (task != null)
            {
                task.CompletionDate = completeTaskCommand.CompletionDate;
                tasksDAO.Update(task);
            }
            else
            {
                throw new ArgumentOutOfRangeException("completeTaskCommand", completeTaskCommand, "Could not find the task to complete");
            }
            return completeTaskCommand;
        }
    }
}