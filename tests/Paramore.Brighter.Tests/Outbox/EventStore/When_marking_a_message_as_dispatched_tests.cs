using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Outbox.EventStore;
using Xunit;

namespace Paramore.Brighter.Tests.OutBox.EventStore
{
    public class EventStoreOutboxMarkDispatchedTests : EventStoreFixture
    {
        [Fact]
        public void When_marking_a_message_as_dispatched_tests()
        {
            // arrange
            var eventStoreOutbox = new EventStoreOutbox(Connection);
            var messageToMarkAsDispatched = CreateMessage(0, StreamName);
            var dispatchedAt = DateTime.UtcNow;
            var args = new Dictionary<string, object> {{EventStoreOutbox.StreamArg, StreamName}};
            
            eventStoreOutbox.Add(messageToMarkAsDispatched);

            // act
            eventStoreOutbox.MarkDispatched(messageToMarkAsDispatched.Id, dispatchedAt, args);

            // assert
            var messages = eventStoreOutbox.Get(1, 2, args);

            messages.Should().ContainSingle().Which.Header.Bag[EventStoreOutbox.DispatchedAtKey].Should().Be(dispatchedAt);
        }
        
        [Fact]
        public void When_null_args_are_supplied()
        {
            // arrange
            var eventStoreOutbox = new EventStoreOutbox(Connection);
            
            // act
            Action getWithoutArgs = () => eventStoreOutbox.MarkDispatched(Guid.Empty, DateTime.UtcNow);
            
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
            // act
            Action getWithoutArgs = () => eventStoreOutbox.MarkDispatched(Guid.Empty, DateTime.UtcNow, args);
            
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
            // act
            Action getWithoutArgs = () => eventStoreOutbox.MarkDispatched(Guid.Empty, DateTime.UtcNow, args);
            
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
            // act
            Action getWithoutArgs = () => eventStoreOutbox.MarkDispatched(Guid.Empty, DateTime.UtcNow, args);
            
            // assert
            getWithoutArgs.Should().Throw<ArgumentException>();
        }
    }
}
