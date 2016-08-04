#region Licence
/* The MIT License (MIT)
Copyright © 2015 Toby Henderson <hendersont@gmail.com>

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
using paramore.brighter.commandprocessor;
using Polly;
using Tasks.Adapters.DataAccess;
using Tasks.Ports;
using Tasks.Ports.Commands;
using Tasks.Ports.Events;
using Tasks.Ports.Handlers;

namespace TasksApi
{
    public class DependencyRegistrar
    {
        
        public void Initialise()
        {
            var container = new TinyIoCContainer();

            var commandProcessor = CommandProcessorRegistrar(container);
        
        }

        private CommandProcessor CommandProcessorRegistrar(TinyIoCContainer container)
        {
            //Database dao
            container.Register<ITasksDAO, TasksDAO>().AsSingleton();

            //create handler 
            var handlerFactory = new TinyIocHandlerFactory(container);
            var subscriberRegistry = new SubscriberRegistry();
            container.Register<IHandleRequests<AddTaskCommand>, AddTaskCommandHandler>().AsMultiInstance();
            subscriberRegistry.Register<AddTaskCommand, AddTaskCommandHandler>();

            //complete handler 
            container.Register<IHandleRequests<CompleteTaskCommand>, CompleteTaskCommandHandler>().AsMultiInstance();
            subscriberRegistry.Register<CompleteTaskCommand, CompleteTaskCommandHandler>();
            
            //create policies
            var retryPolicy = Policy.Handle<Exception>().WaitAndRetry(new[] { TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150) });
            var circuitBreakerPolicy = Policy.Handle<Exception>().CircuitBreaker(1, TimeSpan.FromMilliseconds(500));
            var policyRegistry = new PolicyRegistry() { { CommandProcessor.RETRYPOLICY, retryPolicy }, { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy } };

            //create message mappers
            container.Register<IAmAMessageMapper<TaskReminderCommand>, TaskReminderCommandMessageMapper>().AsMultiInstance();
            var messageMapperFactory = new TinyIoCMessageMapperFactory(container);
            var messageMapperRegistry = new MessageMapperRegistry(messageMapperFactory);
            messageMapperRegistry.Add(typeof(TaskReminderCommand), typeof(TaskReminderCommandMessageMapper));
            messageMapperRegistry.Add(typeof(TaskAddedEvent), typeof(TaskAddedEventMapper));
            messageMapperRegistry.Add(typeof(TaskEditedEvent), typeof(TaskEditedEventMapper));
            messageMapperRegistry.Add(typeof(TaskCompletedEvent), typeof(TaskCompletedEventMapper));
            messageMapperRegistry.Add(typeof(TaskReminderSentEvent), typeof(TaskReminderSentEventMapper));

           
            var gateway = new RmqMessageProducer();
            IAmAMessageStore<Message> sqlMessageStore = new MsSqlMessageStore(new MsSqlMessageStoreConfiguration("Server=.;Database=brighterMessageStore;Trusted_Connection=True", "messages", MsSqlMessageStoreConfiguration.DatabaseType.MsSqlServer));
            var commandProcessor =
                CommandProcessorBuilder.With()
                    .Handlers(new HandlerConfiguration(subscriberRegistry, handlerFactory))
                    .Policies(policyRegistry)
                    .TaskQueues(new MessagingConfiguration(sqlMessageStore, gateway, messageMapperRegistry))
                    .RequestContextFactory(new InMemoryRequestContextFactory())
                    .Build();

            container.Register<IAmACommandProcessor>(commandProcessor);

            return commandProcessor;
        }
 
    }
}