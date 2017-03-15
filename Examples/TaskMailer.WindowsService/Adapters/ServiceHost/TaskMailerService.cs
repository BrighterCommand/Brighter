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
using System.Collections.Generic;
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.RMQ;
using Paramore.Brighter.MessagingGateway.RMQ.MessagingGatewayConfiguration;
using Paramore.Brighter.ServiceActivator;
using Polly;
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
            var rmqConnnection = new RmqMessagingGatewayConnection
            {
                AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
                Exchange = new Exchange("paramore.brighter.exchange"),
            };
            var commandProcessor = CommandProcessorBuilder.With()
                .Handlers(new HandlerConfiguration(subscriberRegistry, handlerFactory))
                .Policies(policyRegistry)
                .TaskQueues(new MessagingConfiguration(new TemporaryMessageStore(), new RmqMessageProducer(rmqConnnection), messageMapperRegistry))
                .RequestContextFactory(new InMemoryRequestContextFactory())
                .Build();

            container.Register<IAmACommandProcessor>(commandProcessor);

            var rmqMessageConsumerFactory = new RmqMessageConsumerFactory(rmqConnnection);
            var rmqMessageProducerFactory = new RmqMessageProducerFactory(rmqConnnection);
            var inputChannelFactory = new InputChannelFactory(new RmqMessageConsumerFactory(rmqConnnection), new RmqMessageProducerFactory(rmqConnnection));
            
            var connections = new List<Connection>
            {
                // Events with mapper and handler overrides
                new Connection(new ConnectionName("Task.Reminder"),inputChannelFactory, typeof(TaskReminderCommand), new ChannelName("Task.Reminder"), "Task.Reminder", noOfPerformers:1, timeoutInMilliseconds: 200),
            };

            _dispatcher = DispatchBuilder.With()
                .CommandProcessor(commandProcessor)
                .MessageMappers(messageMapperRegistry)
                .ChannelFactory(new InputChannelFactory(rmqMessageConsumerFactory, rmqMessageProducerFactory))
                .Connections(connections)
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