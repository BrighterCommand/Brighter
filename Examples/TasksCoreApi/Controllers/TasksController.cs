using System;
using Microsoft.AspNetCore.Mvc;
using paramore.brighter.commandprocessor;
using Tasks.Ports.Commands;
using TasksApi.Resources;
using TasksApi.ViewModelRetrievers;

namespace TasksApi.Controllers
{
    [Route("api/[controller]")]
    public class TasksController : Controller
    {
        private readonly ITaskRetriever _taskRetriever;
        private readonly ITaskListRetriever _taskListRetriever;
        private readonly IAmACommandProcessor _commandProcessor;

        public TasksController(ITaskRetriever taskRetriever, ITaskListRetriever taskListRetriever, IAmACommandProcessor commandProcessor)
        {
            _taskRetriever = taskRetriever;
            _taskListRetriever = taskListRetriever;
            _commandProcessor = commandProcessor;
        }

        [HttpGet]
        public TaskListModel Get()
        {
            TaskListModel responseResource = _taskListRetriever.RetrieveTasks();
            return responseResource;
        }

        [HttpGet("{id}", Name = "GetTask")]
        public TaskModel Get(int id)
        {
            return _taskRetriever.Get(id);
        }

        [HttpPost]
        public IActionResult Post([FromBody]TaskModel newTask)
        {
            var addTaskCommand = new AddTaskCommand(
               taskName: newTask.TaskName,
               taskDescription: newTask.TaskDescription,
               dueDate: DateTime.Parse(newTask.DueDate)
               );

            _commandProcessor.Send(addTaskCommand);

            return this.CreatedAtRoute("GetTask", new {id = addTaskCommand.TaskId}, null);
        }

        [HttpDelete("{id}")]
        public void Delete(int id)
        {
            var completeTaskCommand = new CompleteTaskCommand(
               taskId: id,
               completionDate: DateTime.UtcNow
               );

            _commandProcessor.Send(completeTaskCommand);
        }
    }
}
