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
using System.Threading.Tasks;
using Common.Logging;
using FakeItEasy;
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
        static IAmAMessageChannel channel;
        static IAmACommandProcessor commandProcessor;

        Establish context = () =>
            {
                var container = new TinyIoCAdapter(new TinyIoCContainer());
                channel = new InMemoryChannel();
                commandProcessor = new SpyCommandProcessor();

                var logger = A.Fake<ILog>();
                container.Register<IAmACommandProcessor, IAmACommandProcessor>(commandProcessor);
                container.Register<IAmAMessageMapper<MyEvent>, MyEventMessageMapper>();

                var connection = new Connection(channel: channel, dataType: typeof(MyEvent), noOfPeformers: 1, timeoutInMiliseconds: 1000);
                dispatcher = new Dispatcher(container, new List<Connection>{connection}, logger);

                var @event = new MyEvent();
                var message = new MyEventMessageMapper().MapToMessage(@event);
                channel.Enqueue(message);

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
        private static IAmAMessageChannel channel;
        private static IAmACommandProcessor commandProcessor;

        Establish context = () =>
            {
                var container = new TinyIoCAdapter(new TinyIoCContainer());
                channel = new InMemoryChannel();
                commandProcessor = new SpyCommandProcessor();

                var logger = A.Fake<ILog>();
                container.Register<IAmACommandProcessor, IAmACommandProcessor>(commandProcessor);
                container.Register<IAmAMessageMapper<MyEvent>, MyEventMessageMapper>();

                var connection = new Connection(channel: channel, dataType: typeof (MyEvent), noOfPeformers: 3, timeoutInMiliseconds: 1000);
                dispatcher = new Dispatcher(container, new List<Connection> {connection}, logger);

                var @event = new MyEvent();
                var message = new MyEventMessageMapper().MapToMessage(@event);
                for (var i =0; i < 6; i++)
                    channel.Enqueue(message);

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
        private static IAmAMessageChannel eventChannel;
        private static IAmAMessageChannel commandChannel;
        private static IAmACommandProcessor commandProcessor;

        Establish context = () =>
            {
                var container = new TinyIoCAdapter(new TinyIoCContainer());
                eventChannel = new InMemoryChannel();
                commandChannel = new InMemoryChannel();
                commandProcessor = new SpyCommandProcessor();

                var logger = A.Fake<ILog>();
                container.Register<IAmACommandProcessor, IAmACommandProcessor>(commandProcessor);
                container.Register<IAmAMessageMapper<MyEvent>, MyEventMessageMapper>();
                container.Register<IAmAMessageMapper<MyCommand>, MyCommandMessageMapper>();

                var myEventConnection = new Connection(channel: eventChannel, dataType: typeof (MyEvent), noOfPeformers: 1, timeoutInMiliseconds: 1000);
                var myCommandConnection = new Connection(channel: commandChannel, dataType: typeof (MyCommand), noOfPeformers: 1, timeoutInMiliseconds: 1000);
                dispatcher = new Dispatcher(container, new List<Connection> {myEventConnection, myCommandConnection}, logger);

                var @event = new MyEvent();
                var eventMessage = new MyEventMessageMapper().MapToMessage(@event);
                eventChannel.Enqueue(eventMessage);

                var command = new MyCommand();
                var commandMessage = new MyCommandMessageMapper().MapToMessage(command);
                commandChannel.Enqueue(commandMessage);

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
}
