#region Licence

/* The MIT License (MIT)
Copyright © 2019 Jonny Olliff-Lee <jonny.ollifflee@gmail.com>

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
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Outbox.EventStore;
using Xunit;

namespace Paramore.Brighter.EventStore.Tests.Outbox
{
    [Trait("Category", "EventStore")]
    [Collection("EventStore")]
    public class EventStoreOutboxRangeTests : EventStoreFixture
    {
        [Fact]
        public void When_there_are_multiple_messages_in_the_outbox_and_a_range_is_fetched()
        {
            // arrange
            var eventStoreOutbox = new EventStoreOutbox(Connection);

            var body = new MessageBody("{companyId:123}");
            var header = new MessageHeader(Guid.NewGuid(), "Topic", MessageType.MT_EVENT);
            header.Bag.Add("impersonatorId", 123);
            header.Bag.Add("eventNumber", 0);
            header.Bag.Add("streamId", StreamName);
            var message1 = new Message(header, body);
            
            var body1 = new MessageBody("{companyId:123}");
            var header1 = new MessageHeader(Guid.NewGuid(), "Topic", MessageType.MT_EVENT);
            header1.Bag.Add("impersonatorId", 123);
            header1.Bag.Add("eventNumber", 1);
            header1.Bag.Add("streamId", StreamName);
            var message2 = new Message(header1, body1);
            
            var body2 = new MessageBody("{companyId:123}");
            var header2 = new MessageHeader(Guid.NewGuid(), "Topic", MessageType.MT_EVENT);
            header2.Bag.Add("impersonatorId", 123);
            header2.Bag.Add("eventNumber", 2);
            header2.Bag.Add("streamId", StreamName);
            var message3 = new Message(header2, body2);
            
            eventStoreOutbox.Add(message1);
            Task.Delay(100);
            eventStoreOutbox.Add(message2);
            Task.Delay(100);
            eventStoreOutbox.Add(message3);
            
            var args = new Dictionary<string, object> { { Globals.StreamArg, StreamName }};
            
            // act
            var messages = eventStoreOutbox.Get(1, 3, args);
            
            // assert
            messages.Should().ContainSingle().Which.Should().BeEquivalentTo(message3);
        }
        
        [Fact]
        public void When_null_args_are_supplied()
        {
            // arrange
            var eventStoreOutbox = new EventStoreOutbox(Connection);
            
            // act
            Action getWithoutArgs = () => eventStoreOutbox.Get(1, 1);
            
            // assert
            getWithoutArgs.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void When_null_stream_arg_supplied()
        {
            // arrange
            var eventStoreOutbox = new EventStoreOutbox(Connection);
            var args = new Dictionary<string, object> {{Globals.StreamArg, null}};
            
            // act
            Action getWithoutArgs = () => eventStoreOutbox.Get(1, 1, args);
            
            // assert
            getWithoutArgs.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void When_empty_args_are_supplied()
        {
            // arrange
            var eventStoreOutbox = new EventStoreOutbox(Connection);
            var args = new Dictionary<string, object>();
            
            // act
            Action getWithoutArgs = () => eventStoreOutbox.Get(1, 1, args);
            
            // assert
            getWithoutArgs.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void When_wrong_args_are_supplied()
        {
            // arrange
            var eventStoreOutbox = new EventStoreOutbox(Connection);
            var args = new Dictionary<string, object> { { "Foo", "Bar" }};
            
            // act
            Action getWithoutArgs = () => eventStoreOutbox.Get(1, 1, args);
            
            // assert
            getWithoutArgs.Should().Throw<ArgumentException>();
        }
    }
}
