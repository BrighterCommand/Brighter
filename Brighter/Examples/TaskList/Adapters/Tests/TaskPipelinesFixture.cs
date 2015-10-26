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
using System.Collections.Concurrent;
using FakeItEasy;
using Machine.Specifications;
using paramore.brighter.commandprocessor.Logging;
using Polly;
using Tasks.Adapters.DataAccess;
using Tasks.Model;
using Tasks.Ports.Commands;
using Tasks.Ports.Handlers;
using TinyIoC;
using paramore.brighter.commandprocessor;
using Tasks.Ports.Events;

namespace Tasklist.Adapters.Tests
{
    public class When_I_add_a_new_task
    {
        private static AddTaskCommand s_cmd;
        private static IAmACommandProcessor s_commandProcessor;
        private static RequestContext s_requestContext;

        private Establish _context = () =>
        {
            LogProvider.SetCurrentLogProvider(null);
            var container = new TinyIoCContainer();
            container.Register<ITasksDAO, TasksDAO>();
            container.Register<IHandleRequests<AddTaskCommand>, AddTaskCommandHandler>();
            var handlerFactory = new TinyIocHandlerFactory(container);

            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<AddTaskCommand, AddTaskCommandHandler>();

            var messageMapperRegistry = new MessageMapperRegistry(new TinyIoCMessageMapperFactory(container))
            {
                {typeof (TaskAddedEvent), typeof (TaskAddedEventTestMapper)}
            };

            s_requestContext = new RequestContext();

            var requestContextFactory = A.Fake<IAmARequestContextFactory>();
            A.CallTo(() => requestContextFactory.Create()).Returns(s_requestContext);

            var retryPolicy = Policy.Handle<Exception>().WaitAndRetry(new[] { TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150) });
            var circuitBreakerPolicy = Policy.Handle<Exception>().CircuitBreaker(1, TimeSpan.FromMilliseconds(1));
            var policyRegistry = new PolicyRegistry() { { CommandProcessor.RETRYPOLICY, retryPolicy }, { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy } };

            s_commandProcessor = new CommandProcessor(subscriberRegistry, handlerFactory, requestContextFactory, policyRegistry, messageMapperRegistry, new TestMessageStore(), new TestMessageProducer());
            container.Register<IAmACommandProcessor>(s_commandProcessor);

            s_cmd = new AddTaskCommand("New Task", "Test that we store a task", DateTime.Now.AddDays(3));

            new TasksDAO().Clear();
        };

        private Because _of = () => s_commandProcessor.Send(s_cmd);

        private It _should_store_something_in_the_db = () => FindTaskinDb().ShouldNotBeNull();
        private It _should_store_with_the_correct_name = () => FindTaskinDb().TaskName.ShouldEqual(s_cmd.TaskName);
        private It _should_store_with_the_correct_description = () => FindTaskinDb().TaskDescription.ShouldEqual(s_cmd.TaskDescription);
        private It _should_store_with_the_correct_duedate = () => FindTaskinDb().DueDate.Value.ToShortDateString().ShouldEqual(s_cmd.TaskDueDate.Value.ToShortDateString());

        private static Task FindTaskinDb()
        {
            return new TasksDAO().FindByName(s_cmd.TaskName);
        }
    }

    public class TaskAddedEventTestMapper : IAmAMessageMapper<TaskAddedEvent>
    {
        public Message MapToMessage(TaskAddedEvent request)
        {
            return new Message();
        }

        public TaskAddedEvent MapToRequest(Message message)
        {
            throw new NotImplementedException();
        }
    }

    internal class TestMessageStore : IAmAMessageStore<Message>
    {
        private readonly ConcurrentDictionary<Guid, Message> _store;

        public TestMessageStore()
        {
            this._store = new ConcurrentDictionary<Guid, Message>();
        }

        public void Add(Message message, int messageStoreTimeout = -1)
        {
            this._store.AddOrUpdate(message.Id, message, (guid, oldMessage) => message);
        }

        public Message Get(Guid messageId, int messageStoreTimeout = -1)
        {
            Message message;

            if (!this._store.TryGetValue(messageId, out message))
            {
                throw new ArgumentOutOfRangeException(messageId.ToString());
            }

            return message;
        }
    }

    internal class TestMessageProducer : IAmAMessageProducer
    {
        public void Dispose()
        {
        }

        public void Send(Message message)
        {
        }
    }
}