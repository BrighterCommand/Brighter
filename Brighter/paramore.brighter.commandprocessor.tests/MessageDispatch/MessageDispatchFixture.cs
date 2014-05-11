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

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Logging;
using Common.Logging.Configuration;
using Common.Logging.Simple;
using Machine.Specifications;
using TinyIoC;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.ioccontainers.Adapters;
using paramore.brighter.serviceactivator;
using paramore.brighter.serviceactivator.TestHelpers;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using paramore.commandprocessor.tests.MessageDispatch.TestDoubles;

namespace paramore.commandprocessor.tests.MessageDispatch
{
    public class When_a_message_dispatcher_is_asked_to_connect_a_channel_and_handler
    {
        static Dispatcher dispatcher;
        static IAmAnInputChannel channel;
        static IAmACommandProcessor commandProcessor;

        Establish context = () =>
            {
                var container = new TinyIoCAdapter(new TinyIoCContainer());
                channel = new InMemoryChannel();
                commandProcessor = new SpyCommandProcessor();

                var properties = new NameValueCollection();
                properties["showDateTime"] = "true";
                LogManager.Adapter = new ConsoleOutLoggerFactoryAdapter(properties);

                var logger = LogManager.GetLogger(typeof (Dispatcher));

                container.Register<IAmACommandProcessor, IAmACommandProcessor>(commandProcessor);
                container.Register<IAmAMessageMapper<MyEvent>, MyEventMessageMapper>();
                container.Register<IAmACommandProcessor, IAmACommandProcessor>(commandProcessor);
                container.Register<IAmAMessageMapper<MyEvent>, MyEventMessageMapper>();

                var connection = new Connection(name: new ConnectionName("test"), channel: channel, dataType: typeof(MyEvent), noOfPerformers: 1, timeoutInMilliseconds: 1000);
                dispatcher = new Dispatcher(container, new List<Connection>{connection}, logger);

                var @event = new MyEvent();
                var message = new MyEventMessageMapper().MapToMessage(@event);
                channel.Send(message);

                dispatcher.State.ShouldEqual(DispatcherState.DS_AWAITING);
                dispatcher.Recieve();
            };


            Because of = () =>
                {
                    Task.Delay(1000).Wait();
                    dispatcher.End().Wait();
                };

            It should_have_consumed_the_messages_in_the_channel = () => channel.Length.ShouldEqual(0);
            It should_have_a_stopped_state = () => dispatcher.State.ShouldEqual(DispatcherState.DS_STOPPED);
    }

    public class When_a_message_dispatcher_starts_multiple_performers
    {
        private static Dispatcher dispatcher;
        private static IAmAnInputChannel channel;
        private static IAmACommandProcessor commandProcessor;

        Establish context = () =>
            {
                var container = new TinyIoCAdapter(new TinyIoCContainer());
                channel = new InMemoryChannel();
                commandProcessor = new SpyCommandProcessor();

                var properties = new NameValueCollection();
                properties["showDateTime"] = "true";
                LogManager.Adapter = new ConsoleOutLoggerFactoryAdapter(properties);

                var logger = LogManager.GetLogger(typeof (Dispatcher));

                container.Register<IAmACommandProcessor, IAmACommandProcessor>(commandProcessor);
                container.Register<IAmAMessageMapper<MyEvent>, MyEventMessageMapper>();
                container.Register<IAmACommandProcessor, IAmACommandProcessor>(commandProcessor);
                container.Register<IAmAMessageMapper<MyEvent>, MyEventMessageMapper>();

                var connection = new Connection(name: new ConnectionName("test"), channel: channel, dataType: typeof (MyEvent), noOfPerformers: 3, timeoutInMilliseconds: 1000);
                dispatcher = new Dispatcher(container, new List<Connection> {connection}, logger);

                var @event = new MyEvent();
                var message = new MyEventMessageMapper().MapToMessage(@event);
                for (var i =0; i < 6; i++)
                    channel.Send(message);

                dispatcher.State.ShouldEqual(DispatcherState.DS_AWAITING);
                dispatcher.Recieve();
            };


        Because of = () =>
            {
                Task.Delay(1000).Wait();
                dispatcher.End().Wait();
            };

        It should_have_consumed_the_messages_in_the_channel = () => channel.Length.ShouldEqual(0);
        It should_have_a_stopped_state = () => dispatcher.State.ShouldEqual(DispatcherState.DS_STOPPED);
    }

     public class When_a_message_dispatcher_starts_different_types_of_performers
    {
        private static Dispatcher dispatcher;
        private static IAmAnInputChannel eventChannel;
        private static IAmAnInputChannel commandChannel;
        private static IAmACommandProcessor commandProcessor;

        Establish context = () =>
            {
                var container = new TinyIoCAdapter(new TinyIoCContainer());
                eventChannel = new InMemoryChannel();
                commandChannel = new InMemoryChannel();
                commandProcessor = new SpyCommandProcessor();

                var properties = new NameValueCollection();
                properties["showDateTime"] = "true";
                LogManager.Adapter = new ConsoleOutLoggerFactoryAdapter(properties);

                var logger = LogManager.GetLogger(typeof (Dispatcher));

                container.Register<IAmACommandProcessor, IAmACommandProcessor>(commandProcessor);
                container.Register<IAmAMessageMapper<MyEvent>, MyEventMessageMapper>();
                container.Register<IAmACommandProcessor, IAmACommandProcessor>(commandProcessor);
                container.Register<IAmAMessageMapper<MyEvent>, MyEventMessageMapper>();
                container.Register<IAmAMessageMapper<MyCommand>, MyCommandMessageMapper>();

                var myEventConnection = new Connection(name: new ConnectionName("test"), channel: eventChannel, dataType: typeof (MyEvent), noOfPerformers: 1, timeoutInMilliseconds: 1000);
                var myCommandConnection = new Connection(name: new ConnectionName("anothertest"), channel: commandChannel, dataType: typeof (MyCommand), noOfPerformers: 1, timeoutInMilliseconds: 1000);
                dispatcher = new Dispatcher(container, new List<Connection> {myEventConnection, myCommandConnection}, logger);

                var @event = new MyEvent();
                var eventMessage = new MyEventMessageMapper().MapToMessage(@event);
                eventChannel.Send(eventMessage);

                var command = new MyCommand();
                var commandMessage = new MyCommandMessageMapper().MapToMessage(command);
                commandChannel.Send(commandMessage);

                dispatcher.State.ShouldEqual(DispatcherState.DS_AWAITING);
                dispatcher.Recieve();
            };


        Because of = () =>
            {
                Task.Delay(1000).Wait();
                dispatcher.End().Wait();
            };

        It should_have_consumed_the_messages_in_the_event_channel = () => eventChannel.Length.ShouldEqual(0);
        It should_have_consumed_the_messages_in_the_command_channel = () => commandChannel.Length.ShouldEqual(0);
        It should_have_a_stopped_state = () => dispatcher.State.ShouldEqual(DispatcherState.DS_STOPPED);
    }

    public class When_a_message_dispatcher_shuts_a_connection
    {
        private static Dispatcher dispatcher;
        private static IAmAnInputChannel channel;
        private static IAmACommandProcessor commandProcessor;
        private static Connection connection;

        Establish context = () =>
            {
                var container = new TinyIoCAdapter(new TinyIoCContainer());
                channel = new InMemoryChannel();
                commandProcessor = new SpyCommandProcessor();

                var properties = new NameValueCollection();
                properties["showDateTime"] = "true";
                LogManager.Adapter = new ConsoleOutLoggerFactoryAdapter(properties);

                var logger = LogManager.GetLogger(typeof (Dispatcher));

                container.Register<IAmACommandProcessor, IAmACommandProcessor>(commandProcessor);
                container.Register<IAmAMessageMapper<MyEvent>, MyEventMessageMapper>();

                connection = new Connection(name: new ConnectionName("test"), channel: channel, dataType: typeof (MyEvent), noOfPerformers: 3, timeoutInMilliseconds: 1000);
                dispatcher = new Dispatcher(container, new List<Connection> {connection}, logger);

                var @event = new MyEvent();
                var message = new MyEventMessageMapper().MapToMessage(@event);
                for (var i =0; i < 6; i++)
                    channel.Send(message);

                dispatcher.State.ShouldEqual(DispatcherState.DS_AWAITING);
                dispatcher.Recieve();
            };


        Because of = () =>
            {
                Task.Delay(1000).Wait();
                dispatcher.Shut(connection);
                dispatcher.End().Wait();
            };

        It should_have_consumed_the_messages_in_the_channel = () => dispatcher.Consumers.Any(consumer => (consumer.Name == connection.Name) && (consumer.State == ConsumerState.Open)).ShouldBeFalse(); 
        It should_have_a_stopped_state = () => dispatcher.State.ShouldEqual(DispatcherState.DS_STOPPED);
    }

    public class When_a_message_dispatcher_restarts_a_connection
    {
        private static Dispatcher dispatcher;
        private static IAmAnInputChannel channel;
        private static IAmACommandProcessor commandProcessor;
        private static Connection connection;

        Establish context = () =>
            {
                var container = new TinyIoCAdapter(new TinyIoCContainer());
                channel = new InMemoryChannel();
                commandProcessor = new SpyCommandProcessor();

                var properties = new NameValueCollection();
                properties["showDateTime"] = "true";
                LogManager.Adapter = new ConsoleOutLoggerFactoryAdapter(properties);

                var logger = LogManager.GetLogger(typeof (Dispatcher));

                container.Register<IAmACommandProcessor, IAmACommandProcessor>(commandProcessor);
                container.Register<IAmAMessageMapper<MyEvent>, MyEventMessageMapper>();

                connection = new Connection(name: new ConnectionName("test"), channel: channel, dataType: typeof (MyEvent), noOfPerformers: 1, timeoutInMilliseconds: 1000);
                dispatcher = new Dispatcher(container, new List<Connection> {connection}, logger);

                var @event = new MyEvent();
                var message = new MyEventMessageMapper().MapToMessage(@event);
                channel.Send(message);

                dispatcher.State.ShouldEqual(DispatcherState.DS_AWAITING);
                dispatcher.Recieve();
                Task.Delay(1000).Wait();
                dispatcher.Shut(connection);
            };


        Because of = () =>
            {
                dispatcher.Open(connection);

                var @event = new MyEvent();
                var message = new MyEventMessageMapper().MapToMessage(@event);
                channel.Send(message);

                dispatcher.End().Wait();
            };

        It should_have_consumed_the_messages_in_the_event_channel = () => channel.Length.ShouldEqual(0);
        It should_have_a_stopped_state = () => dispatcher.State.ShouldEqual(DispatcherState.DS_STOPPED);
    }
}
