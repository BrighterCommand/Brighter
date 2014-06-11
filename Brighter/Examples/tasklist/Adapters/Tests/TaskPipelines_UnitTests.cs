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
using Common.Logging;
using FakeItEasy;
using Machine.Specifications;
using Tasks.Adapters.DataAccess;
using Tasks.Model;
using Tasks.Ports.Commands;
using Tasks.Ports.Handlers;
using TinyIoC;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.ioccontainers.Adapters;

namespace Tasklist.Adapters.Tests
{
    [Subject(typeof(AddTaskCommandHandler))]
    public class When_a_new_task_is_missing_the_description
    {
        static AddTaskCommand cmd;
        static IAmACommandProcessor commandProcessor;
        static ITasksDAO tasksDAO;
        static Exception exception;

        Establish context = () =>
        {
            var logger = A.Fake<ILog>();
            tasksDAO = A.Fake<ITasksDAO>();
            A.CallTo(() => tasksDAO.Add(A<Task>.Ignored));
            var log = A.Fake<ILog>();

            IAdaptAnInversionOfControlContainer container = new TinyIoCAdapter(new TinyIoCContainer());
            container.Register<ITasksDAO, ITasksDAO>(tasksDAO);
            container.Register<IHandleRequests<AddTaskCommand>, AddTaskCommandHandler>();
            container.Register<ILog, ILog>(log);

            commandProcessor = new CommandProcessor(container, new InMemoryRequestContextFactory(), logger);

            cmd = new AddTaskCommand("Test task", null);
        };

        Because of = () => exception = Catch.Exception(() => commandProcessor.Send(cmd));

        It should_throw_a_validation_exception = () => exception.ShouldNotBeNull();
        It should_be_of_the_correct_type = () => exception.ShouldBeAssignableTo<ArgumentException>();
        It should_show_a_suitable_message = () => exception.ShouldContainErrorMessage("The commmand was not valid");
    }

    [Subject(typeof(AddTaskCommandHandler))]
    public class When_a_new_task_is_missing_the_name
    {
        static AddTaskCommand cmd;
        static IAmACommandProcessor commandProcessor;
        static ITasksDAO tasksDAO;
        static Exception exception;

        Establish context = () =>
        {
            var logger = A.Fake<ILog>();
            tasksDAO = A.Fake<ITasksDAO>();
            A.CallTo(() => tasksDAO.Add(A<Task>.Ignored));
            var log = A.Fake<ILog>();

            IAdaptAnInversionOfControlContainer container = new TinyIoCAdapter(new TinyIoCContainer());
            container.Register<ITasksDAO, ITasksDAO>(tasksDAO);
            container.Register<IHandleRequests<AddTaskCommand>, AddTaskCommandHandler>();
            container.Register<ILog, ILog>(log);

            commandProcessor = new CommandProcessor(container, new InMemoryRequestContextFactory(), logger);

            cmd = new AddTaskCommand(null, "Test that we store a task");
        };

        Because of = () => exception = Catch.Exception(() => commandProcessor.Send(cmd));

        It should_throw_a_validation_exception = () => exception.ShouldNotBeNull();
        It should_be_of_the_correct_type = () => exception.ShouldBeAssignableTo<ArgumentException>();
        It should_show_a_suitable_message = () => exception.ShouldContainErrorMessage("The commmand was not valid");
    }

    public class When_adding_a_valid_new_task
    {
        static AddTaskCommand cmd;
        static IAmACommandProcessor commandProcessor;
        static ITasksDAO tasksDAO;

        Establish context = () =>
        {
            var logger = A.Fake<ILog>();
            tasksDAO = new TasksDAO();
            tasksDAO.Clear();
            var log = A.Fake<ILog>();

            IAdaptAnInversionOfControlContainer container = new TinyIoCAdapter(new TinyIoCContainer());
            container.Register<ITasksDAO, ITasksDAO>(tasksDAO);
            container.Register<IHandleRequests<AddTaskCommand>, AddTaskCommandHandler>();
            container.Register<ILog, ILog>(log);

            commandProcessor = new CommandProcessor(container, new InMemoryRequestContextFactory(), logger);

            cmd = new AddTaskCommand("Test task", "Test that we store a task", DateTime.Now);
        };

        Because of = () => commandProcessor.Send(cmd);

        It should_add_the_task_to_the_task_List = () => tasksDAO.FindById(cmd.TaskId).ShouldNotBeNull();
    }
}