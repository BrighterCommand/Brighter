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
using System.Threading.Tasks;
using FakeItEasy;
using Machine.Specifications;
using paramore.brighter.commandprocessor.Logging;
using Polly;
using TinyIoC;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.messaginggateway.rmq;
using paramore.brighter.serviceactivator;
using paramore.brighter.serviceactivator.TestHelpers;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using paramore.commandprocessor.tests.MessageDispatch.TestDoubles;

namespace paramore.commandprocessor.tests.MessageDispatch
{
    public class When_a_message_dispatcher_is_asked_to_connect_a_channel_and_handler
    {
        private static Dispatcher s_dispatcher;
        private static FakeChannel s_channel;
        private static IAmACommandProcessor s_commandProcessor;

        private Establish _context = () =>
            {
                s_channel = new FakeChannel();
                s_commandProcessor = new SpyCommandProcessor();

                var logger = LogProvider.For<Dispatcher>();

                var messageMapperRegistry = new MessageMapperRegistry(new TestMessageMapperFactory(() => new MyEventMessageMapper()));
                messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

                var connection = new Connection(name: new ConnectionName("test"), dataType: typeof(MyEvent), noOfPerformers: 1, timeoutInMilliseconds: 1000, channelFactory: new InMemoryChannelFactory(s_channel), channelName: new ChannelName("fakeChannel"), routingKey: "fakekey");
                s_dispatcher = new Dispatcher(s_commandProcessor, messageMapperRegistry, new List<Connection> { connection }, logger);

                var @event = new MyEvent();
                var message = new MyEventMessageMapper().MapToMessage(@event);
                s_channel.Send(message);

                s_dispatcher.State.ShouldEqual(DispatcherState.DS_AWAITING);
                s_dispatcher.Receive();
            };


        private Because _of = () =>
            {
                Task.Delay(1000).Wait();
                s_dispatcher.End().Wait();
            };

        private It _should_have_consumed_the_messages_in_the_channel = () => s_channel.Length.ShouldEqual(0);
        private It _should_have_a_stopped_state = () => s_dispatcher.State.ShouldEqual(DispatcherState.DS_STOPPED);
    }

    public class When_a_message_dispatcher_starts_multiple_performers
    {
        private static Dispatcher s_dispatcher;
        private static FakeChannel s_channel;
        private static IAmACommandProcessor s_commandProcessor;

        private Establish _context = () =>
            {
                s_channel = new FakeChannel();
                s_commandProcessor = new SpyCommandProcessor();

                var logger = LogProvider.For<Dispatcher>();

                var messageMapperRegistry = new MessageMapperRegistry(new TestMessageMapperFactory(() => new MyEventMessageMapper()));
                messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

                var connection = new Connection(name: new ConnectionName("test"), dataType: typeof(MyEvent), noOfPerformers: 3, timeoutInMilliseconds: 1000, channelFactory: new InMemoryChannelFactory(s_channel), channelName: new ChannelName("fakeChannel"), routingKey: "fakekey");
                s_dispatcher = new Dispatcher(s_commandProcessor, messageMapperRegistry, new List<Connection> { connection }, logger);

                var @event = new MyEvent();
                var message = new MyEventMessageMapper().MapToMessage(@event);
                for (var i = 0; i < 6; i++)
                    s_channel.Send(message);

                s_dispatcher.State.ShouldEqual(DispatcherState.DS_AWAITING);
                s_dispatcher.Receive();
            };


        private Because _of = () =>
            {
                Task.Delay(1000).Wait();
                s_dispatcher.End().Wait();
            };

        private It _should_have_consumed_the_messages_in_the_channel = () => s_channel.Length.ShouldEqual(0);
        private It _should_have_a_stopped_state = () => s_dispatcher.State.ShouldEqual(DispatcherState.DS_STOPPED);
    }

    public class When_a_message_dispatcher_starts_different_types_of_performers
    {
        private static Dispatcher s_dispatcher;
        private static FakeChannel s_eventChannel;
        private static FakeChannel s_commandChannel;
        private static IAmACommandProcessor s_commandProcessor;

        private Establish _context = () =>
            {
                s_eventChannel = new FakeChannel();
                s_commandChannel = new FakeChannel();
                s_commandProcessor = new SpyCommandProcessor();

                var logger = LogProvider.For<Dispatcher>();
                var container = new TinyIoCContainer();
                container.Register<MyEventMessageMapper>();
                container.Register<MyCommandMessageMapper>();

                var messageMapperRegistry = new MessageMapperRegistry(new TinyIoCMessageMapperFactory(container));
                messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();
                messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();


                var myEventConnection = new Connection(name: new ConnectionName("test"), dataType: typeof(MyEvent), noOfPerformers: 1, timeoutInMilliseconds: 1000, channelFactory: new InMemoryChannelFactory(s_eventChannel), channelName: new ChannelName("fakeChannel"), routingKey: "fakekey");
                var myCommandConnection = new Connection(name: new ConnectionName("anothertest"), dataType: typeof(MyCommand), noOfPerformers: 1, timeoutInMilliseconds: 1000, channelFactory: new InMemoryChannelFactory(s_commandChannel), channelName: new ChannelName("fakeChannel"), routingKey: "fakekey");
                s_dispatcher = new Dispatcher(s_commandProcessor, messageMapperRegistry, new List<Connection> { myEventConnection, myCommandConnection }, logger);

                var @event = new MyEvent();
                var eventMessage = new MyEventMessageMapper().MapToMessage(@event);
                s_eventChannel.Send(eventMessage);

                var command = new MyCommand();
                var commandMessage = new MyCommandMessageMapper().MapToMessage(command);
                s_commandChannel.Send(commandMessage);

                s_dispatcher.State.ShouldEqual(DispatcherState.DS_AWAITING);
                s_dispatcher.Receive();
            };


        private Because _of = () =>
            {
                Task.Delay(1000).Wait();
                s_numberOfConsumers = s_dispatcher.Consumers.Count();
                s_dispatcher.End().Wait();
            };

        private It _should_have_consumed_the_messages_in_the_event_channel = () => s_eventChannel.Length.ShouldEqual(0);
        private It _should_have_consumed_the_messages_in_the_command_channel = () => s_commandChannel.Length.ShouldEqual(0);
        private It _should_have_a_stopped_state = () => s_dispatcher.State.ShouldEqual(DispatcherState.DS_STOPPED);
        private It _should_have_no_consumers = () => s_dispatcher.Consumers.ShouldBeEmpty();
        private It _should_of_had_2_consumers_when_running = () => s_numberOfConsumers.ShouldEqual(2);

        private static int s_numberOfConsumers;
    }

    public class When_a_message_dispatcher_shuts_a_connection
    {
        private static Dispatcher s_dispatcher;
        private static FakeChannel s_channel;
        private static IAmACommandProcessor s_commandProcessor;
        private static Connection s_connection;

        private Establish _context = () =>
            {
                s_channel = new FakeChannel();
                s_commandProcessor = new SpyCommandProcessor();

                var logger = LogProvider.For<Dispatcher>();

                var messageMapperRegistry = new MessageMapperRegistry(new TestMessageMapperFactory(() => new MyEventMessageMapper()));
                messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

                s_connection = new Connection(name: new ConnectionName("test"), dataType: typeof(MyEvent), noOfPerformers: 3, timeoutInMilliseconds: 1000, channelFactory: new InMemoryChannelFactory(s_channel), channelName: new ChannelName("fakeChannel"), routingKey: "fakekey");
                s_dispatcher = new Dispatcher(s_commandProcessor, messageMapperRegistry, new List<Connection> { s_connection }, logger);

                var @event = new MyEvent();
                var message = new MyEventMessageMapper().MapToMessage(@event);
                for (var i = 0; i < 6; i++)
                    s_channel.Send(message);

                s_dispatcher.State.ShouldEqual(DispatcherState.DS_AWAITING);
                s_dispatcher.Receive();
            };


        private Because _of = () =>
            {
                Task.Delay(1000).Wait();
                s_dispatcher.Shut(s_connection);
                s_dispatcher.End().Wait();
            };

        private It _should_have_consumed_the_messages_in_the_channel = () => s_dispatcher.Consumers.Any(consumer => (consumer.Name == s_connection.Name) && (consumer.State == ConsumerState.Open)).ShouldBeFalse();
        private It _should_have_a_stopped_state = () => s_dispatcher.State.ShouldEqual(DispatcherState.DS_STOPPED);
        private It _should_have_no_consumers = () => s_dispatcher.Consumers.ShouldBeEmpty();
    }

    public class When_a_message_dispatcher_restarts_a_connection
    {
        private static Dispatcher s_dispatcher;
        private static FakeChannel s_channel;
        private static IAmACommandProcessor s_commandProcessor;
        private static Connection s_connection;

        private Establish _context = () =>
            {
                s_channel = new FakeChannel();
                s_commandProcessor = new SpyCommandProcessor();

                var logger = LogProvider.For<Dispatcher>();

                var messageMapperRegistry = new MessageMapperRegistry(new TestMessageMapperFactory(() => new MyEventMessageMapper()));
                messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

                s_connection = new Connection(name: new ConnectionName("test"), dataType: typeof(MyEvent), noOfPerformers: 1, timeoutInMilliseconds: 1000, channelFactory: new InMemoryChannelFactory(s_channel), channelName: new ChannelName("fakeChannel"), routingKey: "fakekey");
                s_dispatcher = new Dispatcher(s_commandProcessor, messageMapperRegistry, new List<Connection> { s_connection }, logger);

                var @event = new MyEvent();
                var message = new MyEventMessageMapper().MapToMessage(@event);
                s_channel.Send(message);

                s_dispatcher.State.ShouldEqual(DispatcherState.DS_AWAITING);
                s_dispatcher.Receive();
                Task.Delay(1000).Wait();
                s_dispatcher.Shut(s_connection);
            };


        private Because _of = () =>
            {
                s_dispatcher.Open(s_connection);

                var @event = new MyEvent();
                var message = new MyEventMessageMapper().MapToMessage(@event);
                s_channel.Send(message);

                s_dispatcher.End().Wait();
            };

        private It _should_have_consumed_the_messages_in_the_event_channel = () => s_channel.Length.ShouldEqual(0);
        private It _should_have_a_stopped_state = () => s_dispatcher.State.ShouldEqual(DispatcherState.DS_STOPPED);
    }

    public class When_a_message_dispatcher_restarts_a_connection_after_all_connections_have_stopped
    {
        private static Dispatcher s_dispatcher;
        private static FakeChannel s_channel;
        private static IAmACommandProcessor s_commandProcessor;
        private static Connection s_connection;
        private static Connection s_newConnection;

        private Establish _context = () =>
        {
            s_channel = new FakeChannel();
            s_commandProcessor = new SpyCommandProcessor();

            var logger = LogProvider.For<Dispatcher>();

            var messageMapperRegistry = new MessageMapperRegistry(new TestMessageMapperFactory(() => new MyEventMessageMapper()));
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

            s_connection = new Connection(name: new ConnectionName("test"), dataType: typeof(MyEvent), noOfPerformers: 1, timeoutInMilliseconds: 1000, channelFactory: new InMemoryChannelFactory(s_channel), channelName: new ChannelName("fakeChannel"), routingKey: "fakekey");
            s_newConnection = new Connection(name: new ConnectionName("newTest"), dataType: typeof(MyEvent), noOfPerformers: 1, timeoutInMilliseconds: 1000, channelFactory: new InMemoryChannelFactory(s_channel), channelName: new ChannelName("fakeChannel"), routingKey: "fakekey");
            s_dispatcher = new Dispatcher(s_commandProcessor, messageMapperRegistry, new List<Connection> { s_connection, s_newConnection }, logger);

            var @event = new MyEvent();
            var message = new MyEventMessageMapper().MapToMessage(@event);
            s_channel.Send(message);

            s_dispatcher.State.ShouldEqual(DispatcherState.DS_AWAITING);
            s_dispatcher.Receive();
            Task.Delay(1000).Wait();
            s_dispatcher.Shut("test");
            s_dispatcher.Shut("newTest");
            Task.Delay(3000).Wait();
            s_dispatcher.Consumers.Count.ShouldEqual(0); //sanity check
        };


        private Because _of = () =>
        {
            s_dispatcher.Open("newTest");
            var @event = new MyEvent();
            var message = new MyEventMessageMapper().MapToMessage(@event);
            s_channel.Send(message);
            Task.Delay(1000).Wait();
        };

        private Cleanup _stop_dispatcher = () => s_dispatcher.End().Wait();

        private It _should_have_consumed_the_messages_in_the_event_channel = () => s_channel.Length.ShouldEqual(0);
        private It _should_have_a_running_state = () => s_dispatcher.State.ShouldEqual(DispatcherState.DS_RUNNING);
        private It _should_have_only_one_consumer = () => s_dispatcher.Consumers.Count.ShouldEqual(1);
    }




    public class When_building_a_dispatcher
    {
        private static IAmADispatchBuilder s_builder;
        private static Dispatcher s_dispatcher;

        private Establish _context = () =>
            {
                var logger = A.Fake<ILog>();
                var messageMapperRegistry = new MessageMapperRegistry(new TestMessageMapperFactory(() => new MyEventMessageMapper()));
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

                //var gateway = new RmqMessageConsumer(logger);
                var rmqMessageConsumerFactory = new RmqMessageConsumerFactory(logger);
                var rmqMessageProducerFactory = new RmqMessageProducerFactory(logger);

                s_builder = DispatchBuilder.With()
                             .Logger(logger)
                             .CommandProcessor(CommandProcessorBuilder.With()
                                .Handlers(new HandlerConfiguration(new SubscriberRegistry(), new TinyIocHandlerFactory(new TinyIoCContainer())))
                                .Policies(new PolicyRegistry() { { CommandProcessor.RETRYPOLICY, retryPolicy }, { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy } })
                                .Logger(logger)
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
    }
}
