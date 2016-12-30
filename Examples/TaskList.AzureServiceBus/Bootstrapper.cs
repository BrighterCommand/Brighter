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
using Nancy.Bootstrapper;
using Nancy.TinyIoc;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messagestore.mssql;
using paramore.brighter.commandprocessor.messaginggateway.azureservicebus;
using Polly;
using TaskList.AzureServiceBus.Adapters;
using TaskList.AzureServiceBus.Adapters.MessageMappers;
using TaskList.AzureServiceBus.Common;
using TaskList.AzureServiceBus.Ports.ViewBuilders;
using TaskList.AzureServiceBus.Ports.ViewModelRetrievers;
using TaskList.AzureServiceBus.Ports.ViewModels;
using TaskList.AzureServiceBus.Ports.Views;
using Tasks.Ports;
using Tasks.Ports.Commands;
using Tasks.Ports.Events;
using Tasks.Ports.Handlers;

namespace TaskList.AzureServiceBus
{
    public class Bootstrapper : DefaultNancyBootstrapper
    {
        protected override void ConfigureApplicationContainer(TinyIoCContainer container)
        {
            base.ConfigureApplicationContainer(container);
            var logger = LogProvider.GetLogger(this.GetType().Namespace);
            container.Register(logger);

            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<AddTaskCommand, AddTaskCommandHandler>();
            subscriberRegistry.Register<CompleteTaskCommand, CompleteTaskCommandHandler>();

            container.Register<IHandleRequests<AddTaskCommand>, AddTaskCommandHandler>().AsMultiInstance();
            container.Register<IHandleRequests<CompleteTaskCommand>, CompleteTaskCommandHandler>().AsMultiInstance();
            var handlerFactory = new TinyIoCHandlerFactory(container);

            var retryPolicy = Policy.Handle<Exception>().WaitAndRetry(new[] { TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150) });
            var circuitBreakerPolicy = Policy.Handle<Exception>().CircuitBreaker(1, TimeSpan.FromMilliseconds(500));
            var policyRegistry = new PolicyRegistry() { { CommandProcessor.RETRYPOLICY, retryPolicy }, { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy } };

            var sqlMessageStore = new MsSqlMessageStore(new MsSqlMessageStoreConfiguration("Data Source=|DataDirectory|Tasks.sdf;Persist Security Info=False;", "messages", MsSqlMessageStoreConfiguration.DatabaseType.SqlCe), logger);
            var gateway = new AzureServiceBusMessageProducer(logger);

            container.Register<IAmAMessageMapper<TaskReminderCommand>, TaskReminderCommandMessageMapper>().AsMultiInstance();
            var messageMapperFactory = new TinyIoCMessageMapperFactory(container);
            var messageMapperRegistry = new MessageMapperRegistry(messageMapperFactory);
            messageMapperRegistry.Add(typeof(TaskReminderCommand), typeof(TaskReminderCommandMessageMapper));
            messageMapperRegistry.Add(typeof(TaskCompletedEvent), typeof(TaskCompletedEventMapper));

            var commandProcessor = CommandProcessorBuilder
                .With()
                .Handlers(new HandlerConfiguration(subscriberRegistry, handlerFactory))
                    .Policies(policyRegistry)
                    .TaskQueues(new MessagingConfiguration(sqlMessageStore, gateway, messageMapperRegistry))
                    .RequestContextFactory(new InMemoryRequestContextFactory())
                    .Build();
            container.Register<IAmACommandProcessor>(commandProcessor);
        }

        protected override void ConfigureRequestContainer(TinyIoCContainer container, NancyContext context)
        {
            base.ConfigureRequestContainer(container, context);
            container.Register<IRetrieveTaskViewModels, TaskViewModelRetriever>();
            container.Register<IProvideBaseUris>((c, o) => new BaseUriProvider(context));
            container.Register<IBuildViews<TaskViewModel, TaskView>, TaskViewBuilder>();
        }

        protected override void ApplicationStartup(TinyIoCContainer container, IPipelines pipelines)
        {
            Conventions.ViewLocationConventions.Add((viewName, model, context) => string.Concat("Ports/Views/", viewName));
        }
    }
}