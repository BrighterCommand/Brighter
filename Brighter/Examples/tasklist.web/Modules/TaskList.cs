using Nancy;
using tasklist.web.ViewModelRetrievers;

namespace tasklist.web.Modules
{
    public class TaskListModule : NancyModule
    {
        private readonly ITaskListRetriever taskListRetriever;

        public TaskListModule(ITaskListRetriever taskListRetriever)
        {
            this.taskListRetriever = taskListRetriever;
            Get["/todo/index"] = _ =>
            {
                 var tasks = taskListRetriever.RetrieveTasks();
                 return View["index.sshtml", new { Tasks = tasks }];
            };
        }
    }
}