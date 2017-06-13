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
using FluentAssertions;
using Xunit;
using Paramore.Brighter.MessagingGateway.RMQ;
using Paramore.Brighter.MessagingGateway.RMQ.MessagingGatewayConfiguration;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Tests.MessageDispatch.TestDoubles;
using Polly;
using TinyIoC;

namespace Paramore.Brighter.Tests.MessageDispatch
{
    public class DispatchBuilderWithNamedGateway
    {
        private readonly IAmADispatchBuilder _builder;
        private Dispatcher _dispatcher;

        public DispatchBuilderWithNamedGateway()
        {
            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(() => new MyEventMessageMapper()));
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();
            var policyRegistry = new PolicyRegistry
            {
                {
                    CommandProcessor.RETRYPOLICY, Policy
                        .Handle<Exception>()
                        .WaitAndRetry(new[] {TimeSpan.FromMilliseconds(50)})
                },
                {
                    CommandProcessor.CIRCUITBREAKER, Policy
                        .Handle<Exception>()
                        .CircuitBreaker(1, TimeSpan.FromMilliseconds(500))
                }
            };

            var connection = new RmqMessagingGatewayConnection
            {
                AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
                Exchange = new Exchange("paramore.brighter.exchange")
            };

            var rmqMessageConsumerFactory = new RmqMessageConsumerFactory(connection);
            var rmqMessageProducerFactory = new RmqMessageProducerFactory(connection);

            var connections = new List<Connection>
            {
                new Connection(
                    new ConnectionName("foo"),
                    new InputChannelFactory(rmqMessageConsumerFactory, rmqMessageProducerFactory),
                    typeof(MyEvent),
                    new ChannelName("mary"),
                    new RoutingKey("bob"),
                    timeoutInMilliseconds: 200),
                new Connection(
                    new ConnectionName("bar"),
                    new InputChannelFactory(rmqMessageConsumerFactory, rmqMessageProducerFactory),
                    typeof(MyEvent),
                    new ChannelName("alice"),
                    new RoutingKey("simon"),
                    timeoutInMilliseconds: 200)
            };

            _builder = DispatchBuilder.With()
                .CommandProcessor(CommandProcessorBuilder.With()
                        .Handlers(new HandlerConfiguration(new SubscriberRegistry(),
                            new TinyIocHandlerFactory(new TinyIoCContainer())))
                        .Policies(policyRegistry)
                        .NoTaskQueues()
                        .RequestContextFactory(new InMemoryRequestContextFactory())
                        .Build()
                )
                .MessageMappers(messageMapperRegistry)
                .ChannelFactory(new InputChannelFactory(rmqMessageConsumerFactory, rmqMessageProducerFactory))
                .Connections(connections);
        }

        [Fact]
        public void When_building_a_dispatcher_with_named_gateway()
        {
            _dispatcher = _builder.Build();

            //_should_build_a_dispatcher
            _dispatcher.Should().NotBeNull();
        }
    }
}
