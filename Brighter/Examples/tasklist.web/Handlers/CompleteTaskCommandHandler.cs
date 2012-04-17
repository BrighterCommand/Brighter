using paramore.commandprocessor;
using tasklist.web.Commands;
using tasklist.web.DataAccess;

namespace tasklist.web.Handlers
{
    public class CompleteTaskCommandHandler : RequestHandler<CompleteTaskCommand>
    {
        readonly ITasksDAO tasksDAO;

        public CompleteTaskCommandHandler(ITasksDAO tasksDao)
        {
            tasksDAO = tasksDao;
        }

        [Validation(step: 2, timing: HandlerTiming.Before)]
        [Trace(step:1, timing: HandlerTiming.Before)]
        public override CompleteTaskCommand Handle(CompleteTaskCommand completeTaskCommand)
        {
            //get from repo
            //update completion date
            //save to repo
            return completeTaskCommand;
        }
    }
}