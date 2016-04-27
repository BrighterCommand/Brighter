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
using System.IO;
using System.Reflection;
using ManagementAndMonitoring.Ports.CommandHandlers;
using ManagementAndMonitoring.Ports.Commands;
using ManagementAndMonitoring.Ports.Mappers;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.messagestore.mssql;
using paramore.brighter.commandprocessor.messaginggateway.rmq;
using paramore.brighter.serviceactivator;
using paramore.brighter.serviceactivator.controlbus;
using Polly;
using TinyIoC;
using Topshelf;

namespace ManagementAndMonitoring.Adapters.ServiceHost
{
    internal class ManagementAndMonitoringService : ServiceControl
    {
        private Dispatcher _dispatcher;
        private readonly Dispatcher _controlDispatcher;

        public ManagementAndMonitoringService()
        {

            log4net.Config.XmlConfigurator.Configure();

            var container = new TinyIoCContainer();

            var handlerFactory = new TinyIocHandlerFactory(container);
            var messageMapperFactory = new TinyIoCMessageMapperFactory(container);
            container.Register<IHandleRequests<GreetingCommand>, GreetingCommandHandler>();

            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<GreetingCommand, GreetingCommandHandler>();

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

            //create message mappers
            var messageMapperRegistry = new MessageMapperRegistry(messageMapperFactory)
            {
                {typeof(GreetingCommand), typeof(GreetingCommandMessageMapper)}
            };

            //create the gateway
            var rmqMessageConsumerFactory = new RmqMessageConsumerFactory("messages");
            var rmqMessageProducerFactory = new RmqMessageProducerFactory("messages");

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
                 .ChannelFactory(new InputChannelFactory(rmqMessageConsumerFactory, rmqMessageProducerFactory))
                 .ConnectionsFromConfiguration();    
            _dispatcher = builder.Build();

            var controlBusBuilder = ControlBusReceiverBuilder
                .With()
                .Dispatcher(_dispatcher)
                .ProducerFactory(rmqMessageProducerFactory)
                .ChannelFactory(new InputChannelFactory(rmqMessageConsumerFactory, rmqMessageProducerFactory)) as ControlBusReceiverBuilder;
            _controlDispatcher = controlBusBuilder.Build(Environment.MachineName + "." + "ManagementAndMonitoring");

            container.Register<IAmAControlBusSender>(new ControlBusSenderFactory().Create(
                new MsSqlMessageStore(
                    new MsSqlMessageStoreConfiguration(
                    "DataSource=\"" + Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetName().CodeBase.Substring(8)), "App_Data\\MessageStore.sdf") + "\"", "Messages", 
                    MsSqlMessageStoreConfiguration.DatabaseType.SqlCe)
                    ), 
                new RmqMessageProducer("monitoring")));
        }

        public bool Start(HostControl hostControl)
        {
            _controlDispatcher.Receive();
            _dispatcher.Receive();
            return true;
        }

        public bool Stop(HostControl hostControl)
        {
            _controlDispatcher.End();   //Don't wait on the control bus, we are stopping so we don't want any more control messages, just terminate
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