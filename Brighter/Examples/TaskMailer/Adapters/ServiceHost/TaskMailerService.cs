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
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messaginggateway.rmq;
using paramore.brighter.serviceactivator;
using Polly;
using TaskMailer.Ports;
using Tasks.Adapters.MailGateway;
using Tasks.Ports;
using Tasks.Ports.Commands;
using Tasks.Ports.Events;
using Tasks.Ports.Handlers;
using TinyIoC;
using Topshelf;

namespace TaskMailer.Adapters.ServiceHost
{
    internal class TaskMailerService : ServiceControl
    {
        private Dispatcher _dispatcher;

        public TaskMailerService()
        {
            log4net.Config.XmlConfigurator.Configure();

            var container = new TinyIoCContainer();
            container.Register<IAmAMessageMapper<TaskReminderCommand>, Tasks.Ports.TaskReminderCommandMessageMapper>();
            container.Register<MailTaskReminderHandler, MailTaskReminderHandler>();
            container.Register<IAmAMailGateway, MailGateway>();
            container.Register<IAmAMailTranslator, MailTranslator>();

            var handlerFactory = new TinyIocHandlerFactory(container);
            var messageMapperFactory = new TinyIoCMessageMapperFactory(container);

            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<TaskReminderCommand, MailTaskReminderHandler>();

            //create policies
            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetry(new[]
                    {
                        TimeSpan.FromMilliseconds(50),
                        TimeSpan.FromMilliseconds(100),
                        TimeSpan.FromMilliseconds(150)
                    });

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(1, TimeSpan.FromMilliseconds(500));

            var policyRegistry = new PolicyRegistry()
            {
                {CommandProcessor.RETRYPOLICY, retryPolicy},
                {CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy}
            };

            var messageMapperRegistry = new MessageMapperRegistry(messageMapperFactory)
            {
                {typeof (TaskReminderCommand), typeof (Tasks.Ports.TaskReminderCommandMessageMapper)},
                {typeof (TaskReminderSentEvent), typeof (TaskMailer.Ports.TaskReminderSentEventMapper)}
            };

            var commandProcessor = CommandProcessorBuilder.With()
                .Handlers(new HandlerConfiguration(subscriberRegistry, handlerFactory))
                .Policies(policyRegistry)
                .TaskQueues(new MessagingConfiguration(new TemporaryMessageStore(), new RmqMessageProducer(), messageMapperRegistry))
                .RequestContextFactory(new InMemoryRequestContextFactory())
                .Build();

            container.Register<IAmACommandProcessor>(commandProcessor);

            var rmqMessageConsumerFactory = new RmqMessageConsumerFactory();
            var rmqMessageProducerFactory = new RmqMessageProducerFactory();

            _dispatcher = DispatchBuilder.With()
                .CommandProcessor(commandProcessor)
                .MessageMappers(messageMapperRegistry)
                .ChannelFactory(new InputChannelFactory(rmqMessageConsumerFactory, rmqMessageProducerFactory))
                .ConnectionsFromConfiguration()
                .Build();
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
            return true;
        }

        public void Shutdown(HostControl hostcontrol)
        {
            if (_dispatcher != null)
                _dispatcher.End().Wait();
        }
    }

    internal class TemporaryMessageStore : IAmAMessageStore<Message>
    {
        private readonly ConcurrentDictionary<Guid, Message> _store;

        public TemporaryMessageStore()
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
}