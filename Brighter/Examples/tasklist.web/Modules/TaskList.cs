using Nancy;
using tasklist.web.Commands;
using tasklist.web.ViewModelRetrievers;

namespace tasklist.web.Modules
{
    public class TaskListModule : NancyModule
    {
        private readonly ITaskListRetriever taskListRetriever;

        public TaskListModule(ITaskListRetriever taskListRetriever)
        {
            this.taskListRetriever = taskListRetriever;
            Get["/todo/index"] = _ => TasksView(); 

            Post["/todo/index"] = _ =>
            {
                var cmd = new AddTaskCommand(Request.Form.taskName, Request.Form.taskDecription);
                return TasksView();
            };

        }
        private Response TasksView()
        {
            return View["index.sshtml", new {Tasks = taskListRetriever.RetrieveTasks()}];
          }
    }
}