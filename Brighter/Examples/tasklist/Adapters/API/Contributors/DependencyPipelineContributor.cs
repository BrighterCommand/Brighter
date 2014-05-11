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
using FluentAssertions;
using OpenRasta.DI;
using OpenRasta.Pipeline;
using OpenRasta.Web;
using Polly;
using Tasklist.Adapters.DataAccess;
using Tasklist.Ports.Commands;
using Tasklist.Ports.Handlers;
using Tasklist.Ports.ViewModelRetrievers;
using TinyIoC;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.ioccontainers.Adapters;
using paramore.brighter.commandprocessor.messagestore.ravendb;
using paramore.brighter.commandprocessor.messaginggateway.rmq;

namespace Tasklist.Adapters.API.Contributors
{
    public class DependencyPipelineContributor : IPipelineContributor
    {
        private readonly IDependencyResolver resolver;

        public DependencyPipelineContributor(IDependencyResolver resolver)
        {
            this.resolver = resolver;
        }

        public void Initialize(IPipeline pipelineRunner)
        {
            pipelineRunner.Notify(InitializeContainer)
                .Before<KnownStages.IOperationExecution>();
        }

        private PipelineContinuation InitializeContainer(ICommunicationContext arg)
        {
            var container = new TinyIoCAdapter(new TinyIoCContainer());
            //HACK! For now dependencies may need to be in both containers to allow resolution
            container.Register<IHandleRequests<AddTaskCommand>, AddTaskCommandHandler>().AsMultiInstance();
            container.Register<ITaskListRetriever, TaskListRetriever>().AsMultiInstance();
            container.Register<ITasksDAO, TasksDAO>().AsMultiInstance();
            var logger = LogManager.GetLogger("TaskList");
            container.Register<ILog, ILog>(logger);
            container.Register<IAmARequestContextFactory, InMemoryRequestContextFactory>().AsMultiInstance();
            MessageStoreFactory.InstallRavenDbMessageStore(container);
            container.Register<IAmAMessageStore<Message>, RavenMessageStore>().AsSingleton();
            container.Register<IAmAMessagingGateway, RMQMessagingGateway>().AsSingleton();
            container.Register<Policy>(CommandProcessor.RETRYPOLICY, GetRetryPolicy());
            container.Register<Policy>(CommandProcessor.CIRCUITBREAKER, GetCircuitBreakerPolicy());

            var commandProcessor = new CommandProcessorFactory(container).Create();
            container.Register<IAmACommandProcessor, IAmACommandProcessor>(commandProcessor);

            resolver.AddDependencyInstance<IAdaptAnInversionOfControlContainer>(container, DependencyLifetime.Singleton);
            resolver.AddDependencyInstance<IAmARequestContextFactory>(new InMemoryRequestContextFactory(), DependencyLifetime.PerRequest);
            resolver.AddDependencyInstance<IAmACommandProcessor>(commandProcessor, DependencyLifetime.Singleton);
            resolver.AddDependency<ITaskRetriever, TaskRetriever>(DependencyLifetime.Singleton);
            resolver.AddDependency<ITaskListRetriever, TaskListRetriever>(DependencyLifetime.Singleton);


            return PipelineContinuation.Continue;
        }

        private Policy GetCircuitBreakerPolicy()
        {
            return Policy
                .Handle<Exception>()
                .CircuitBreaker(2, TimeSpan.FromMilliseconds(500));
        }

        private Policy GetRetryPolicy()
        {
            return Policy
                .Handle<Exception>()
                .WaitAndRetry(new[]
                {
                    1.Seconds(),
                    2.Seconds(),
                    3.Seconds()
                }, (exception, timeSpan) =>
                    {
                        var logger = LogManager.GetLogger("RetryPolicy");
                        logger.Error(m => m("Error during decoupled invocation attempt: {0}, retrying in {1)", exception, timeSpan));
                });
        }
    }
}