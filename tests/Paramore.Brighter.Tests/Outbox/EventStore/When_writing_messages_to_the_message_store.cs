#region Licence
/* The MIT License (MIT)
Copyright © 2014 Francesco Pighi <francesco.pighi@gmail.com>

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

using System.ComponentModel;
using System.Linq;
using FluentAssertions;
using Paramore.Brighter.Outbox.EventStore;
using Xunit;

namespace Paramore.Brighter.Tests.OutBox.EventStore
{
    [Category("EventStore")]
    public class EventStoreOutboxTests : EventStoreFixture
    {
        [Fact]
        public void When_Writing_Messages_To_The_Outbox_Async()
        {
            // arrange
            var eventStoreOutbox = new EventStoreOutbox(Connection);

            var message1 = CreateMessage(0, StreamName);
            var message2 = CreateMessage(1, StreamName);
            
            // act
            eventStoreOutbox.Add(message1);
            eventStoreOutbox.Add(message2);   
            
            // assert
            var messages = eventStoreOutbox.Get(StreamName, 0, 2);

            messages.Count(m => MessagesEqualApartFromTimestamp(m, message1)).Should().Be(1);
            messages.Count(m => MessagesEqualApartFromTimestamp(m, message2)).Should().Be(1);
        }
    }
}
