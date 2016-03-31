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
using System.Linq;
using FakeItEasy;
using Machine.Specifications;
using paramore.brighter.commandprocessor.Logging;
using Polly;
using TinyIoC;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.messaginggateway.rmq;
using paramore.brighter.serviceactivator;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using paramore.commandprocessor.tests.MessageDispatch.TestDoubles;

namespace paramore.commandprocessor.tests.MessageDispatch
{
    [Subject(typeof(DispatchBuilder))]
    public class When_Building_A_Dispatcher
    {
        /*
        <connections>
          <add connectionName ="foo" channelName="mary" routingKey="bob" isDurable="false" dataType="paramore.commandprocessor.tests.CommandProcessors.TestDoubles.MyEvent" noOfPerformers="1" timeOutInMilliseconds="200" />
          <add connectionName ="bar" channelName="alice" routingKey="simon" isDurable="true" dataType="paramore.commandprocessor.tests.CommandProcessors.TestDoubles.MyEvent" noOfPerformers="2" timeOutInMilliseconds="100" isAsync="true" />
        </connections>
        */
        private static IAmADispatchBuilder s_builder;
        private static Dispatcher s_dispatcher;

        private Establish _context = () =>
            {
                var logger = A.Fake<ILog>();
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

                var rmqMessageConsumerFactory = new RmqMessageConsumerFactory(logger);
                var rmqMessageProducerFactory = new RmqMessageProducerFactory(logger);

                s_builder = DispatchBuilder.With()
                             .CommandProcessor(CommandProcessorBuilder.With()
                                .Handlers(new HandlerConfiguration(new SubscriberRegistry(), new TinyIocHandlerFactory(new TinyIoCContainer())))
                                .Policies(new PolicyRegistry() { { CommandProcessor.RETRYPOLICY, retryPolicy }, { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy } })
                                .NoTaskQueues()
                                 .RequestContextFactory(new InMemoryRequestContextFactory())
                                .Build()
                                )
                             .MessageMappers(messageMapperRegistry)
                             .ChannelFactory(new InputChannelFactory(rmqMessageConsumerFactory, rmqMessageProducerFactory))
                             .ConnectionsFromConfiguration();
            };

        private Because _of = () => s_dispatcher = s_builder.Build();

        private It _should_build_a_dispatcher = () => s_dispatcher.ShouldNotBeNull();
        private It _should_have_a_foo_connection = () => GetConnection("foo").ShouldNotBeNull();
        private It _should_have_a_bar_connection = () => GetConnection("bar").ShouldNotBeNull();
        private It _should_be_in_the_awaiting_state = () => s_dispatcher.State.ShouldEqual(DispatcherState.DS_AWAITING);


        private static Connection GetConnection(string name)
        {
            return s_dispatcher.Connections.SingleOrDefault(conn => conn.Name == name);
        }

    }
}
