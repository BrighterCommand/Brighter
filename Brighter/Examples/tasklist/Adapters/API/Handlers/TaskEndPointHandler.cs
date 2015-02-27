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

using System;
using OpenRasta.Web;
using Tasklist.Adapters.API.Resources;
using Tasklist.Ports.ViewModelRetrievers;
using paramore.brighter.commandprocessor;
using Tasks.Ports.Commands;

namespace Tasklist.Adapters.API.Handlers
{
    public class TaskEndPointHandler
    {
        private readonly ITaskRetriever _taskRetriever;
        private readonly IAmACommandProcessor _commandProcessor;
        private readonly ICommunicationContext _communicationContext;
        private readonly ITaskListRetriever _taskListRetriever;

        public TaskEndPointHandler(ITaskRetriever taskRetriever, ITaskListRetriever taskListRetriever, IAmACommandProcessor commandProcessor, ICommunicationContext communicationContext)
        {
            _taskRetriever = taskRetriever;
            _taskListRetriever = taskListRetriever;
            _commandProcessor = commandProcessor;
            _communicationContext = communicationContext;
        }

        [HttpOperation(HttpMethod.GET)]
        public OperationResult Get()
        {
            TaskListModel responseResource = _taskListRetriever.RetrieveTasks();
            return new OperationResult.OK { ResponseResource = responseResource };
        }

        [HttpOperation(HttpMethod.GET)]
        public OperationResult Get(int taskId)
        {
            return new OperationResult.OK { ResponseResource = _taskRetriever.Get(taskId) };
        }

        [HttpOperation(HttpMethod.POST)]
        public OperationResult Post(TaskModel newTask)
        {
            var addTaskCommand = new AddTaskCommand(
                taskName: newTask.TaskName,
                taskDecription: newTask.TaskDescription,
                dueDate: DateTime.Parse(newTask.DueDate)
                );

            _commandProcessor.Send(addTaskCommand);

            return new OperationResult.Created
            {
                RedirectLocation = new Uri(string.Format("{0}/tasks/{1}", _communicationContext.ApplicationBaseUri, addTaskCommand.TaskId))
            };
        }
    }
}