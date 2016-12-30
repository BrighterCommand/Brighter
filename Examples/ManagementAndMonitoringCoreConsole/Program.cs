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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Polly;
using Serilog;
using ManagementAndMonitoring.ManualTinyIoc;
using ManagementAndMonitoring.Adapters.ServiceHost;
using ManagementAndMonitoring.Ports.Commands;
using paramore.brighter.commandprocessor;
using ManagementAndMonitoring.Ports.CommandHandlers;
using ManagementAndMonitoring.Ports.Mappers;
using paramore.brighter.commandprocessor.messaginggateway.rmq;
using paramore.brighter.serviceactivator;
using paramore.brighter.serviceactivator.controlbus;
using Connection = paramore.brighter.serviceactivator.Connection;
using paramore.brighter.commandprocessor.messaginggateway.rmq.MessagingGatewayConfiguration;
using paramore.brighter.commandprocessor.messagestore.sqlite;

namespace ManagementAndMonitoringCoreConsole
{
    internal class Program
    {
        private static Dispatcher _dispatcher;

        public static void Main()
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();

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

            var rmqGatewayMessages = RmqGatewayBuilder.With
                .Uri(new Uri("amqp://guest:guest@localhost:5672/%2f"))
                .Exchange("paramore.brighter.exchange")
                .DefaultQueues();

            var rmqMessageProducerFactory = new RmqMessageProducerFactory(rmqGatewayMessages);

            var inputChannelFactory = new InputChannelFactory(new RmqMessageConsumerFactory(rmqGatewayMessages), rmqMessageProducerFactory);
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
                 .ChannelFactory(inputChannelFactory)
                 .Connections(new[]
                 {
                     ConnectionBuilder.With
                        .Name("paramore.example.greeting")
                        .ChannelFactory(inputChannelFactory)
                        .Type(typeof(GreetingCommand))
                        .ChannelName("greeting.event")
                        .RoutingKey("greeting.event")
                        .Timeout(200)
                        .Build()
                 });
            _dispatcher = builder.Build();

            var controlBusBuilder = ControlBusReceiverBuilder
                .With()
                .Dispatcher(_dispatcher)
                .ProducerFactory(rmqMessageProducerFactory)
                .ChannelFactory(inputChannelFactory) as ControlBusReceiverBuilder;
            var controlDispatcher = controlBusBuilder.Build(Environment.MachineName + "." + "ManagementAndMonitoring");

            container.Register<IAmAControlBusSender>(new ControlBusSenderFactory().Create(
                new SqliteMessageStore(
                    new SqliteMessageStoreConfiguration(
                    "DataSource=\"" + Path.Combine(Path.GetDirectoryName(typeof(GreetingCommand).GetTypeInfo().Assembly.CodeBase.Substring(8)), "App_Data\\MessageStore.sdf") + "\"", "Messages")
                    ),
                new RmqMessageProducer(RmqGatewayBuilder.With
                    .Uri(new Uri("amqp://guest:guest@localhost:5672/%2f"))
                    .Exchange("paramore.brighter.exchange")
                    .DefaultQueues())));

            controlDispatcher.Receive();
            _dispatcher.Receive();

            Console.WriteLine("Press Enter to stop ...");
            Console.ReadLine();

            controlDispatcher.End();   //Don't wait on the control bus, we are stopping so we don't want any more control messages, just terminate
            _dispatcher.End().Wait();
        }
    }
}
