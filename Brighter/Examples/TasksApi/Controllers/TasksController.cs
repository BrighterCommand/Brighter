using System;
using System.Collections.Generic;
using System.Linq;
using Tasks.Model;
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

        // GET api/values
        [HttpGet]
        public TaskListModel Get()
        {
            TaskListModel responseResource = _taskListRetriever.RetrieveTasks();
            return responseResource;
        }

        // GET api/values/5
        [HttpGet("{id}")]
        public TaskModel Get(int id)
        {
            return _taskRetriever.Get(id);
        }

        // POST api/values
        [HttpPost]
        public void Post([FromBody]TaskModel newTask)
        {
            var addTaskCommand = new AddTaskCommand(
               taskName: newTask.TaskName,
               taskDescription: newTask.TaskDescription,
               dueDate: DateTime.Parse(newTask.DueDate)
               );

            _commandProcessor.Send(addTaskCommand);
        }

        // PUT api/values/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
