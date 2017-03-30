#region Licence
/* The MIT License (MIT)
Copyright � 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the �Software�), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED �AS IS�, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
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
using FluentAssertions;
using Xunit;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.TestHelpers;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Tests.MessageDispatch.TestDoubles;
using TinyIoC;

namespace Paramore.Brighter.Tests.MessageDispatch
{

    public class MessageDispatcherMultipleConnectionTests
    {
        private Dispatcher _dispatcher;
        private FakeChannel _eventChannel;
        private FakeChannel _commandChannel;
        private IAmACommandProcessor _commandProcessor;
        private static int _numberOfConsumers;

        public MessageDispatcherMultipleConnectionTests()
        {
            _eventChannel = new FakeChannel();
            _commandChannel = new FakeChannel();
            _commandProcessor = new SpyCommandProcessor();

            var container = new TinyIoCContainer();
            container.Register<MyEventMessageMapper>();
            container.Register<MyCommandMessageMapper>();

            var messageMapperRegistry = new MessageMapperRegistry(new TinyIoCMessageMapperFactory(container));
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();


            var myEventConnection = new Connection(name: new ConnectionName("test"), dataType: typeof(MyEvent), noOfPerformers: 1, timeoutInMilliseconds: 1000, channelFactory: new InMemoryChannelFactory(_eventChannel), channelName: new ChannelName("fakeChannel"), routingKey: "fakekey");
            var myCommandConnection = new Connection(name: new ConnectionName("anothertest"), dataType: typeof(MyCommand), noOfPerformers: 1, timeoutInMilliseconds: 1000, channelFactory: new InMemoryChannelFactory(_commandChannel), channelName: new ChannelName("fakeChannel"), routingKey: "fakekey");
            _dispatcher = new Dispatcher(_commandProcessor, messageMapperRegistry, new List<Connection> { myEventConnection, myCommandConnection });

            var @event = new MyEvent();
            var eventMessage = new MyEventMessageMapper().MapToMessage(@event);
            _eventChannel.Add(eventMessage);

            var command = new MyCommand();
            var commandMessage = new MyCommandMessageMapper().MapToMessage(command);
            _commandChannel.Add(commandMessage);

            _dispatcher.State.Should().Be(DispatcherState.DS_AWAITING);
            _dispatcher.Receive();
        }


        [Fact]
        public void When_A_Message_Dispatcher_Starts_Different_Types_Of_Performers()
        {
            Task.Delay(1000).Wait();
            _numberOfConsumers = _dispatcher.Consumers.Count();
            _dispatcher.End().Wait();

           //_should_have_consumed_the_messages_in_the_event_channel
            _eventChannel.Length.Should().Be(0);
            //_should_have_consumed_the_messages_in_the_command_channel
            _commandChannel.Length.Should().Be(0);
            //_should_have_a_stopped_state
            _dispatcher.State.Should().Be(DispatcherState.DS_STOPPED);
            //_should_have_no_consumers
            _dispatcher.Consumers.Any().Should().BeFalse();
            //_should_of_had_2_consumers_when_running
            _numberOfConsumers.Should().Be(2);
        }

    }
}