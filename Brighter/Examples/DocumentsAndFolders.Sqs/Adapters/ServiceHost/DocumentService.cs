﻿#region Licence
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

using DocumentsAndFolders.Sqs.Ports.Events;

using Greetings.Adapters.ServiceHost;
using Greetings.Ports.CommandHandlers;
using Greetings.Ports.Mappers;

using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messaginggateway.awssqs;
using paramore.brighter.serviceactivator;

using Polly;

using TinyIoC;

using Topshelf;

namespace DocumentsAndFolders.Sqs.Adapters.ServiceHost
{
    internal class DocumentService : ServiceControl
    {
        private Dispatcher _dispatcher;

        public DocumentService()
        {
            log4net.Config.XmlConfigurator.Configure();
            

            var container = new TinyIoCContainer();

            var handlerFactory = new TinyIocHandlerFactory(container);
            var messageMapperFactory = new TinyIoCMessageMapperFactory(container);

            container.Register<IHandleRequests<DocumentCreatedEvent>, DocumentCreatedEventHandler>();
            container.Register<IHandleRequests<DocumentUpdatedEvent>, DocumentUpdatedEventHandler>();
            container.Register<IHandleRequests<FolderCreatedEvent>, FolderCreatedEventHandler>();

            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<DocumentCreatedEvent, DocumentCreatedEventHandler>();
            subscriberRegistry.Register<DocumentUpdatedEvent, DocumentUpdatedEventHandler>();
            subscriberRegistry.Register<FolderCreatedEvent, FolderCreatedEventHandler>();

            //create policies
            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetry(new[]
                    {
                        TimeSpan.FromMilliseconds(5000),
                        TimeSpan.FromMilliseconds(10000),
                        TimeSpan.FromMilliseconds(10000)
                    });

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(1, TimeSpan.FromMilliseconds(500));

            var policyRegistry = new PolicyRegistry()
            {
                {CommandProcessor.RETRYPOLICY, retryPolicy},
                {CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy}
            };

            //create message mappers
            var messageMapperRegistry = new MessageMapperRegistry(messageMapperFactory)
            {
                {typeof(FolderCreatedEvent), typeof(FolderCreatedEventMessageMapper)},
                {typeof(DocumentCreatedEvent), typeof(DocumentCreatedEventMessageMapper)},
                {typeof(DocumentUpdatedEvent), typeof(DocumentUpdatedEventMessageMapper)}
            };
            
            var sqsMessageConsumerFactory = new SqsMessageConsumerFactory();
            var sqsMessageProducerFactory = new SqsMessageProducerFactory();

            var builder = DispatchBuilder
                .With()
                .CommandProcessor(CommandProcessorBuilder.With()
                    .Handlers(new HandlerConfiguration(subscriberRegistry, handlerFactory))
                    .Policies(policyRegistry)
                    .NoTaskQueues()
                    .RequestContextFactory(new InMemoryRequestContextFactory())
                    .Build()
                 )
                 .MessageMappers(messageMapperRegistry)
                 .ChannelFactory(new InputChannelFactory(sqsMessageConsumerFactory, sqsMessageProducerFactory))
                 .ConnectionsFromConfiguration();
            _dispatcher = builder.Build();
        }

        public bool Start(HostControl hostControl)
        {
            _dispatcher.Receive();
            return true;
        }

        public bool Stop(HostControl hostControl)
        {
            _dispatcher.End().Wait();
            _dispatcher = null;
            return false;
        }

        public void Shutdown(HostControl hostcontrol)
        {
            if (_dispatcher != null)
                _dispatcher.End();
            return;
        }
    }
}