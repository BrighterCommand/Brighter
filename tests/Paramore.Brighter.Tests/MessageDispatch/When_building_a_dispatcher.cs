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
using System.Linq;
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
    public class DispatchBuilderTests
    {
        private readonly IAmADispatchBuilder _builder;
        private Dispatcher _dispatcher;

        public DispatchBuilderTests()
        {
            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(() => new MyEventMessageMapper()));
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

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

            var rmqConnection = new RmqMessagingGatewayConnection
            {
                AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
                Exchange = new Exchange("paramore.brighter.exchange")
            };

            var rmqMessageConsumerFactory = new RmqMessageConsumerFactory(rmqConnection);
            var rmqMessageProducerFactory = new RmqMessageProducerFactory(rmqConnection);

            var commandProcessor = CommandProcessorBuilder.With()
                .Handlers(new HandlerConfiguration(new SubscriberRegistry(), new TinyIocHandlerFactory(new TinyIoCContainer())))
                .Policies(new PolicyRegistry
                {
                    { CommandProcessor.RETRYPOLICY, retryPolicy },
                    { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy }
                })
                .NoTaskQueues()
                .RequestContextFactory(new InMemoryRequestContextFactory())
                .Build();

            _builder = DispatchBuilder.With()
                .CommandProcessor(commandProcessor)
                .MessageMappers(messageMapperRegistry)
                .DefaultChannelFactory(new InputChannelFactory(rmqMessageConsumerFactory, rmqMessageProducerFactory))
                .Connections(new []
                {
                    new Connection<MyEvent>(
                        new ConnectionName("foo"),
                        new ChannelName("mary"),
                        new RoutingKey("bob"),
                        timeoutInMilliseconds: 200),
                    new Connection<MyEvent>(
                        new ConnectionName("bar"),
                        new ChannelName("alice"),
                        new RoutingKey("simon"),
                        timeoutInMilliseconds: 200)
                });
        }

        [Fact]
        public void When_Building_A_Dispatcher()
        {
            _dispatcher = _builder.Build();

            //_should_build_a_dispatcher
            _dispatcher.Should().NotBeNull();
            //_should_have_a_foo_connection
            GetConnection("foo").Should().NotBeNull();
            //_should_have_a_bar_connection
            GetConnection("bar").Should().NotBeNull();
            //_should_be_in_the_awaiting_state
            _dispatcher.State.Should().Be(DispatcherState.DS_AWAITING);
        }

        private Connection GetConnection(string name)
        {
            return _dispatcher.Connections.SingleOrDefault(conn => conn.Name == name);
        }
    }
}
