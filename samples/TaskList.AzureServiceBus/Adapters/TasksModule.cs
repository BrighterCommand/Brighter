#region Licence
/* The MIT License (MIT)
Copyright © 2015 Yiannis Triantafyllopoulos <yiannis.triantafyllopoulos@gmail.com>

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
using Nancy;
using paramore.brighter.commandprocessor;
using TaskList.AzureServiceBus.Ports.ViewBuilders;
using TaskList.AzureServiceBus.Ports.ViewModelRetrievers;
using TaskList.AzureServiceBus.Ports.ViewModels;
using TaskList.AzureServiceBus.Ports.Views;
using Tasks.Ports.Commands;

namespace TaskList.AzureServiceBus.Adapters
{
    public class TasksModule : NancyModule
    {
        private readonly IRetrieveTaskViewModels taskViewModelRetriever;
        private readonly IBuildViews<TaskViewModel, TaskView> taskViewBuilder;
        private readonly IAmACommandProcessor commandProcessor;

        public TasksModule(IRetrieveTaskViewModels taskViewModelRetriever,
            IBuildViews<TaskViewModel, TaskView> taskViewBuilder,
            IAmACommandProcessor commandProcessor)
        {
            this.taskViewModelRetriever = taskViewModelRetriever;
            this.taskViewBuilder = taskViewBuilder;
            this.commandProcessor = commandProcessor;

            Get["/tasks/{id}"] = _ =>
            {
                var viewModel = taskViewModelRetriever.Get(_.id);
                return taskViewBuilder.Build(viewModel);
            };

            Delete["/tasks/{id}"] = _ =>
            {
                var command = new CompleteTaskCommand(_.id, DateTime.UtcNow);
                commandProcessor.Send(command);
                return Negotiate.WithStatusCode(HttpStatusCode.OK);
            };
        }
    }
}