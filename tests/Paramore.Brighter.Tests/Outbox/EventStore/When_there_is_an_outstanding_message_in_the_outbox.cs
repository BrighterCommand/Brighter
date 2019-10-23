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
    public class OutStandingMessageTests : EventStoreFixture
    {
        [Fact]
        public void When_there_is_an_outstanding_message_in_the_outbox()
        {
            // arrange
            var eventStoreOutbox = new EventStoreOutbox(Connection);
            
            var args = new Dictionary<string, object> {{EventStoreOutbox.StreamArg, StreamName}};
            
            var outstandingMessage = CreateMessage(0, StreamName);
            var dispatchedMessage = CreateMessage(1, StreamName);
            var outstandingRecentMessage = CreateMessage(3, StreamName);
            
            eventStoreOutbox.Add(outstandingMessage);
            eventStoreOutbox.Add(dispatchedMessage);
            Task.Delay(1000).Wait();
            eventStoreOutbox.MarkDispatched(dispatchedMessage.Id, DateTime.UtcNow, args);
            eventStoreOutbox.Add(outstandingRecentMessage);

            // act
            var messages = eventStoreOutbox.OutstandingMessages(500, 100, 1, args);

            // assert
            messages.Should().BeEquivalentTo(outstandingMessage);
        }
        
        [Fact]
        public void When_null_args_are_supplied()
        {
            // arrange
            var eventStoreOutbox = new EventStoreOutbox(Connection);
            
            // act
            Action getWithoutArgs = () => eventStoreOutbox.OutstandingMessages(500, 100, 1);
            
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
            Action getWithoutArgs = () => eventStoreOutbox.OutstandingMessages(500, 100, 1, args);
            
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
            Action getWithoutArgs = () => eventStoreOutbox.OutstandingMessages(500, 100, 1, args);
            
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
            Action getWithoutArgs = () => eventStoreOutbox.OutstandingMessages(500, 100, 1, args);
            
            // assert
            getWithoutArgs.Should().Throw<ArgumentException>();
        }
    }
}
