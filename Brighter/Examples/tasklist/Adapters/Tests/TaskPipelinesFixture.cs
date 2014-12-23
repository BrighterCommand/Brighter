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
using System.Web.UI.WebControls;
using Common.Logging;
using FakeItEasy;
using Machine.Specifications;
using Tasks.Adapters.DataAccess;
using Tasks.Model;
using Tasks.Ports.Commands;
using Tasks.Ports.Handlers;
using TinyIoC;
using paramore.brighter.commandprocessor;

namespace Tasklist.Adapters.Tests
{
    public class When_I_add_a_new_task
    {
        static AddTaskCommand cmd;
        static IAmACommandProcessor commandProcessor;
        static RequestContext requestContext; 

        Establish context = () =>
        {
            var container = new TinyIoCContainer();
            container.Register<ITasksDAO, TasksDAO>();
            container.Register<IHandleRequests<AddTaskCommand>, AddTaskCommandHandler>();
            container.Register<ILog, ILog>(A.Fake<ILog>());
            var handlerFactory = new TinyIocHandlerFactory(container);

            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<AddTaskCommand, AddTaskCommandHandler>();

            requestContext = new RequestContext();

            var logger = A.Fake<ILog>();
            var requestContextFactory = A.Fake<IAmARequestContextFactory>();
            A.CallTo(() => requestContextFactory.Create()).Returns(requestContext);

            commandProcessor = new CommandProcessor(subscriberRegistry, handlerFactory, requestContextFactory, new PolicyRegistry(), logger);

            cmd = new AddTaskCommand("New Task", "Test that we store a task", DateTime.Now.AddDays(3));

            new TasksDAO().Clear();
        };

        Because of = () => commandProcessor.Send(cmd);

        It should_store_something_in_the_db = () => FindTaskinDb().ShouldNotBeNull();
        It should_store_with_the_correct_name = () => FindTaskinDb().TaskName.ShouldEqual(cmd.TaskName);
        It should_store_with_the_correct_description = () => FindTaskinDb().TaskDescription.ShouldEqual(cmd.TaskDecription);
        It should_store_with_the_correct_duedate = () => FindTaskinDb().DueDate.Value.ToShortDateString().ShouldEqual(cmd.TaskDueDate.Value.ToShortDateString());

        static Task FindTaskinDb()
        {
            return new TasksDAO().FindByName(cmd.TaskName);
        }
    }
}