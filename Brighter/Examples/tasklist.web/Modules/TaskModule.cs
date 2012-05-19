using System;
using Nancy;
using paramore.commandprocessor;
using tasklist.web.Commands;
using tasklist.web.Tests;
using tasklist.web.ViewModelRetrievers;

namespace tasklist.web.Modules
{
    public class TaskModule : NancyModule
    {
        private readonly ITaskListRetriever taskListRetriever;
        private readonly ITaskRetriever taskRetriever;
        private readonly IAmACommandProcessor commandProcessor;

        public TaskModule(ITaskListRetriever taskListRetriever, ITaskRetriever taskRetriever, IAmACommandProcessor commandProcessor)
        {
            this.taskListRetriever = taskListRetriever;
            this.taskRetriever = taskRetriever;
            this.commandProcessor = commandProcessor;

            Get["/todo/index"] = _ => TasksView();
            Get["/todo/task/{id}"] = parameters => TaskDetails(parameters.id);
            Get["/todo/add"] = _ => TaskForm();
            Post["/todo/add"] = _ => AddTask();
            Get["/todo/task/{id}"] = parameters => TaskUpdate(parameters.id);

        }

        private Response TaskUpdate(int id)
        {
            return View["EditTask.sshtml", new {Task = taskRetriever.Get(id)}];
        }

        private Response TaskDetails(int id)
        {
            return View["TaskDetail.sshtml", new {Task = taskRetriever.Get(id)}];
        }

        private Response TaskForm()
        {
            return View["AddTask.sshtml"];
        }

        private Response AddTask()
        {
            var cmd = new AddTaskCommand(
                Request.Form.taskName, 
                Request.Form.taskDecription, 
                DateTime.Parse(Request.Form.taskDueDate)
                );
            commandProcessor.Send(cmd);
            return TasksView();
        }

        private Response TasksView()
        {
            return View["Index.sshtml", new {Tasks = taskListRetriever.RetrieveTasks()}];
        }
    }
}