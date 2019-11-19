using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Outbox.EventStore;
using Xunit;

namespace Paramore.Brighter.Tests.OutBox.EventStore
{
    [Trait("Category", "EventStore")]
    [Collection("EventStore Outbox")]
    public class EventStoreOutboxRangeTests : EventStoreFixture
    {
        [Fact]
        public void When_there_are_multiple_messages_in_the_outbox_and_a_range_is_fetched()
        {
            // arrange
            var eventStoreOutbox = new EventStoreOutbox(Connection);

            var message1 = CreateMessage(0, StreamName);
            var message2 = CreateMessage(1, StreamName);
            var message3 = CreateMessage(2, StreamName);
            
            eventStoreOutbox.Add(message1);
            Task.Delay(100);
            eventStoreOutbox.Add(message2);
            Task.Delay(100);
            eventStoreOutbox.Add(message3);
            
            var args = new Dictionary<string, object> { { EventStoreOutbox.StreamArg, StreamName }};
            
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
            var args = new Dictionary<string, object> {{EventStoreOutbox.StreamArg, null}};
            
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
