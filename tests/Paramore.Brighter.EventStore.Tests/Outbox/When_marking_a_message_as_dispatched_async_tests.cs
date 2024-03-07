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
using Xunit.Abstractions;

namespace Paramore.Brighter.EventStore.Tests.Outbox
{
    [Trait("Category", "EventStore")]
    [Collection("EventStore")]
    public class EventStoreOutboxMarkDispatchedAsyncTests(EventStoreFixture fixture) 
    {
        [Fact]
        public async Task When_marking_a_message_as_dispatched_async_tests()
        {
            // arrange
            var eventStoreOutbox = new EventStoreOutbox(fixture.Connection);
            string streamName = $"{Guid.NewGuid()}";
            var body = new MessageBody("{companyId:123}");
            var header = new MessageHeader(Guid.NewGuid(), "Topic", MessageType.MT_EVENT);
            header.Bag.Add("impersonatorId", 123);
            header.Bag.Add("eventNumber", 0);
            header.Bag.Add("streamId", streamName);
            
            var messageToMarkAsDispatched = new Message(header, body);
            
            var dispatchedAt = DateTime.UtcNow;
            var args = new Dictionary<string, object> {{Globals.StreamArg, streamName}};
            
            await eventStoreOutbox.AddAsync(messageToMarkAsDispatched);

            // act
            await eventStoreOutbox.MarkDispatchedAsync(messageToMarkAsDispatched.Id, dispatchedAt, args);

            // assert
            var messages = await eventStoreOutbox.DispatchedMessagesAsync(0);

            messages.Should().ContainSingle().Which.Header.Bag[Globals.DispatchedAtKey].Should().Be(dispatchedAt);
        }
        
        [Fact]
        public void When_null_args_are_supplied()
        {
            // arrange
            var eventStoreOutbox = new EventStoreOutbox(fixture.Connection);
            
            // act
            Func<Task> getWithoutArgs = async () => await eventStoreOutbox.MarkDispatchedAsync(Guid.Empty, DateTime.UtcNow);
            
            // assert
            getWithoutArgs.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public void When_null_stream_arg_supplied()
        {
            // arrange
            var eventStoreOutbox = new EventStoreOutbox(fixture.Connection);
            var args = new Dictionary<string, object> {{Globals.StreamArg, null}};
            
            // act
            // act
            Func<Task> getWithoutArgs = async () => await eventStoreOutbox.MarkDispatchedAsync(Guid.Empty, DateTime.UtcNow, args);
            
            // assert
            getWithoutArgs.Should().ThrowAsync<ArgumentException>();
        }

        [Fact]
        public void When_empty_args_are_supplied()
        {
            // arrange
            var eventStoreOutbox = new EventStoreOutbox(fixture.Connection);
            var args = new Dictionary<string, object>();
            
            // act
            // act
            Func<Task> getWithoutArgs = async () => await eventStoreOutbox.MarkDispatchedAsync(Guid.Empty, DateTime.UtcNow, args);
            
            // assert
            getWithoutArgs.Should().ThrowAsync<ArgumentException>();
        }

        [Fact]
        public void When_wrong_args_are_supplied()
        {
            // arrange
            var eventStoreOutbox = new EventStoreOutbox(fixture.Connection);
            var args = new Dictionary<string, object> { { "Foo", "Bar" }};
            
            // act
            // act
            Func<Task> getWithoutArgs = async () => await eventStoreOutbox.MarkDispatchedAsync(Guid.Empty, DateTime.UtcNow, args);
            
            // assert
            getWithoutArgs.Should().ThrowAsync<ArgumentException>();
        }
    }
}
