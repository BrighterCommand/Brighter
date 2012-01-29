using Nancy;
using paramore.commandprocessor;
using tasklist.web.Commands;
using tasklist.web.ViewModelRetrievers;

namespace tasklist.web.Modules
{
    public class TaskListModule : NancyModule
    {
        private readonly ITaskListRetriever taskListRetriever;
        private readonly IAmACommandProcessor commandProcessor;

        public TaskListModule(ITaskListRetriever taskListRetriever, IAmACommandProcessor commandProcessor)
        {
            this.taskListRetriever = taskListRetriever;
            this.commandProcessor = commandProcessor;

            Get["/todo/index"] = _ => TasksView(); 
            Post["/todo/index"] = _ => AddTask();

        }

        private Response AddTask()
        {
            var cmd = new AddTaskCommand(Request.Form.taskName, Request.Form.taskDecription);
            commandProcessor.Send(cmd);
            return TasksView();
        }

        private Response TasksView()
        {
            return View["Index.sshtml", new {Tasks = taskListRetriever.RetrieveTasks()}];
        }
    }
}