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
using FakeItEasy;
using Machine.Specifications;
using paramore.brighter.commandprocessor.Logging;
using Polly;
using Tasks.Adapters.DataAccess;
using Tasks.Model;
using Tasks.Ports.Commands;
using Tasks.Ports.Handlers;
using paramore.brighter.commandprocessor;
using Tasks.Ports.Events;
using TinyIoC;

namespace Tasklist.Adapters.Tests
{
    [Subject(typeof(AddTaskCommandHandler))]
    public class When_a_new_task_is_missing_the_description
    {
        private static AddTaskCommand s_cmd;
        private static IAmACommandProcessor s_commandProcessor;
        private static ITasksDAO s_tasksDAO;
        private static Exception s_exception;

        private Establish _context = () =>
        {
            LogProvider.SetCurrentLogProvider(null);
            s_tasksDAO = A.Fake<ITasksDAO>();
            A.CallTo(() => s_tasksDAO.Add(A<Task>.Ignored));

            var container = new TinyIoCContainer();
            container.Register<ITasksDAO, ITasksDAO>(s_tasksDAO);
            container.Register<IHandleRequests<AddTaskCommand>, AddTaskCommandHandler>();
            var handlerFactory = new TinyIocHandlerFactory(container);

            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<AddTaskCommand, AddTaskCommandHandler>();

            var retryPolicy = Policy.Handle<Exception>().WaitAndRetry(new[] { TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150) });
            var policyRegistry = new PolicyRegistry() { { CommandProcessor.RETRYPOLICY, retryPolicy } };

            s_commandProcessor = new CommandProcessor(subscriberRegistry, handlerFactory, new InMemoryRequestContextFactory(), policyRegistry);
            container.Register<IAmACommandProcessor>(s_commandProcessor);

            s_cmd = new AddTaskCommand("Test task", null);
        };

        private Because _of = () => s_exception = Catch.Exception(() => s_commandProcessor.Send(s_cmd));

        private It _should_throw_a_validation_exception = () => s_exception.ShouldNotBeNull();
        private It _should_be_of_the_correct_type = () => s_exception.ShouldBeAssignableTo<ArgumentException>();
        private It _should_show_a_suitable_message = () => s_exception.ShouldContainErrorMessage("The commmand was not valid");
    }

    [Subject(typeof(AddTaskCommandHandler))]
    public class When_a_new_task_is_missing_the_name
    {
        private static AddTaskCommand s_cmd;
        private static IAmACommandProcessor s_commandProcessor;
        private static ITasksDAO s_tasksDAO;
        private static Exception s_exception;

        private Establish _context = () =>
        {
            LogProvider.SetCurrentLogProvider(null);
            s_tasksDAO = A.Fake<ITasksDAO>();
            A.CallTo(() => s_tasksDAO.Add(A<Task>.Ignored));

            var container = new TinyIoCContainer();
            container.Register<ITasksDAO, ITasksDAO>(s_tasksDAO);
            container.Register<IHandleRequests<AddTaskCommand>, AddTaskCommandHandler>();
            var handlerFactory = new TinyIocHandlerFactory(container);

            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<AddTaskCommand, AddTaskCommandHandler>();

            var retryPolicy = Policy.Handle<Exception>().WaitAndRetry(new[] { TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150) });
            var policyRegistry = new PolicyRegistry() { { CommandProcessor.RETRYPOLICY, retryPolicy } };

            s_commandProcessor = new CommandProcessor(subscriberRegistry, handlerFactory, new InMemoryRequestContextFactory(), policyRegistry);
            container.Register<IAmACommandProcessor>(s_commandProcessor);

            s_cmd = new AddTaskCommand(null, "Test that we store a task");
        };

        private Because _of = () => s_exception = Catch.Exception(() => s_commandProcessor.Send(s_cmd));

        private It _should_throw_a_validation_exception = () => s_exception.ShouldNotBeNull();
        private It _should_be_of_the_correct_type = () => s_exception.ShouldBeAssignableTo<ArgumentException>();
        private It _should_show_a_suitable_message = () => s_exception.ShouldContainErrorMessage("The commmand was not valid");
    }

    public class When_adding_a_valid_new_task
    {
        private static AddTaskCommand s_cmd;
        private static IAmACommandProcessor s_commandProcessor;
        private static ITasksDAO s_tasksDAO;

        private Establish _context = () =>
        {
            LogProvider.SetCurrentLogProvider(null);
            s_tasksDAO = new TasksDAO();
            s_tasksDAO.Clear();

            var container = new TinyIoCContainer();
            container.Register<ITasksDAO, ITasksDAO>(s_tasksDAO);
            container.Register<IHandleRequests<AddTaskCommand>, AddTaskCommandHandler>();
            var handlerFactory = new TinyIocHandlerFactory(container);

            var messageMapperRegistry = new MessageMapperRegistry(new TinyIoCMessageMapperFactory(container));
            messageMapperRegistry.Add(typeof(TaskAddedEvent), typeof(TaskAddedEventTestMapper));

            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<AddTaskCommand, AddTaskCommandHandler>();

            var retryPolicy = Policy.Handle<Exception>().WaitAndRetry(new[] { TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150) });
            var circuitBreakerPolicy = Policy.Handle<Exception>().CircuitBreaker(1, TimeSpan.FromMilliseconds(1));
            var policyRegistry = new PolicyRegistry() { { CommandProcessor.RETRYPOLICY, retryPolicy }, { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy } };

            s_commandProcessor = new CommandProcessor(subscriberRegistry, handlerFactory, new InMemoryRequestContextFactory(), policyRegistry, messageMapperRegistry, new TestMessageStore(), new TestMessageProducer());
            container.Register<IAmACommandProcessor>(s_commandProcessor);

            s_cmd = new AddTaskCommand("Test task", "Test that we store a task", DateTime.Now);
        };

        private Because _of = () => s_commandProcessor.Send(s_cmd);

        private It _should_add_the_task_to_the_task_List = () => s_tasksDAO.FindById(s_cmd.TaskId).ShouldNotBeNull();
    }
}