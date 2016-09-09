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
using Amazon.Runtime;
using DocumentsAndFolders.Sqs.Core.Ports.CommandHandlers;
using DocumentsAndFolders.Sqs.Core.Ports.Events;
using DocumentsAndFolders.Sqs.Core.Ports.Mappers;
using Greetings.Adapters.ServiceHost;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.messaginggateway.awssqs;
using paramore.brighter.serviceactivator;
using paramore.brighter.serviceactivator.Configuration;
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

            var awsCredentials = new StoredProfileAWSCredentials();


            var sqsMessageConsumerFactory = new SqsMessageConsumerFactory(awsCredentials );
            var sqsMessageProducerFactory = new SqsMessageProducerFactory(awsCredentials );

            var subscriptions = new Subscriptions();
            subscriptions.Add(new ConnectionElement {ConnectionName = "paramore.example.documentsandfolders.documentcreatedevent", ChannelName = "https://sqs.eu-west-1.amazonaws.com/027649620536/DocumentCreatedEvent", RoutingKey = "DocumentCreatedEvent", DataType = "DocumentsAndFolders.Sqs.Core.Ports.Events.DocumentCreatedEvent", TimeoutInMiliseconds = 5000, NoOfPerformers = 10});
            subscriptions.Add(new ConnectionElement {ConnectionName = "paramore.example.documentsandfolders.documentupdatedevent", ChannelName = "https://sqs.eu-west-1.amazonaws.com/027649620536/DocumentUpdatedEvent", RoutingKey = "DocumentUpdatedEvent", DataType = "DocumentsAndFolders.Sqs.Core.Ports.Events.DocumentUpdatedEvent", TimeoutInMiliseconds = 5000, NoOfPerformers = 10});
            subscriptions.Add(new ConnectionElement {ConnectionName = "paramore.example.documentsandfolders.foldercreateddevent", ChannelName = "https://sqs.eu-west-1.amazonaws.com/027649620536/FolderCreatedEvent", RoutingKey = "FolderCreatedEvent", DataType = "DocumentsAndFolders.Sqs.Ports.Core.Events.FolderCreatedEvent", TimeoutInMiliseconds = 5000, NoOfPerformers = 10});

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
                .ConnectionsFromSubscriptions(subscriptions);
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