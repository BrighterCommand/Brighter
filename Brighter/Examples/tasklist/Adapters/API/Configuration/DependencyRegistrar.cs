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

using OpenRasta.DI;

using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messagestore.ravendb;
using paramore.brighter.commandprocessor.messaginggateway.rmq;

using Polly;

using Raven.Client.Embedded;

using Tasklist.Ports.ViewModelRetrievers;

using Tasks.Adapters.DataAccess;
using Tasks.Ports;
using Tasks.Ports.Commands;
using Tasks.Ports.Handlers;

using TinyIoC;

namespace Tasklist.Adapters.API.Configuration
{
    public class DependencyRegistrar
    {
        private InternalDependencyResolver _internalDependencyResolver;

        public void Initialise()
        {
            var logger = LogProvider.GetLogger("TaskList");
            var container = new TinyIoCContainer();
            container.Register<ILog, ILog>(logger);

            var commandProcessor = CommandProcessorRegistrar(container, logger);
            OpenRastaRegistrar(commandProcessor);
        }

        private CommandProcessor CommandProcessorRegistrar(TinyIoCContainer container, ILog logger)
        {
            //Database dao
            container.Register<ITasksDAO, TasksDAO>().AsSingleton();

            //create handler 
            container.Register<IHandleRequests<AddTaskCommand>, AddTaskCommandHandler>().AsMultiInstance();
            var handlerFactory = new TinyIocHandlerFactory(container);
            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<AddTaskCommand, AddTaskCommandHandler>();

            //create policies
            var retryPolicy = Policy.Handle<Exception>().WaitAndRetry(new[] { TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150) });
            var circuitBreakerPolicy = Policy.Handle<Exception>().CircuitBreaker(1, TimeSpan.FromMilliseconds(500));
            var policyRegistry = new PolicyRegistry() { { CommandProcessor.RETRYPOLICY, retryPolicy }, { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy } };

            //create message mappers
            container.Register<IAmAMessageMapper<TaskReminderCommand>, TaskReminderCommandMessageMapper>().AsMultiInstance();
            var messageMapperFactory = new TinyIoCMessageMapperFactory(container);
            var messageMapperRegistry = new MessageMapperRegistry(messageMapperFactory);
            messageMapperRegistry.Add(typeof(TaskReminderCommand), typeof(TaskReminderCommandMessageMapper));

            var ravenMessageStore = new RavenMessageStore(new EmbeddableDocumentStore().Initialize(), logger);
            var gateway = new RmqMessageProducer(logger);

            var commandProcessor =
                CommandProcessorBuilder.With()
                    .Handlers(new HandlerConfiguration(subscriberRegistry, handlerFactory))
                    .Policies(policyRegistry)
                    .Logger(logger)
                    .TaskQueues(new MessagingConfiguration(ravenMessageStore, gateway, messageMapperRegistry))
                    .RequestContextFactory(new InMemoryRequestContextFactory())
                    .Build();

            return commandProcessor;
        }

        private void OpenRastaRegistrar(CommandProcessor commandProcessor)
        {
            _internalDependencyResolver = new InternalDependencyResolver();

            _internalDependencyResolver.AddDependencyInstance<IAmACommandProcessor>(commandProcessor, DependencyLifetime.Singleton);
            _internalDependencyResolver.AddDependency<ITaskRetriever, TaskRetriever>(DependencyLifetime.Singleton);
            _internalDependencyResolver.AddDependency<ITaskListRetriever, TaskListRetriever>(DependencyLifetime.Singleton);
        }


        public IDependencyResolver GetDependencyResolver()
        {
            return _internalDependencyResolver;
        }
    }
}