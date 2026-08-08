#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.OnceOnly
{
    public class InMemoryOutboxCausationTrackingTests
    {
        private const string CausationA = "causation-A";
        private const string CausationB = "causation-B";

        private readonly FakeTimeProvider _timeProvider = new();
        private readonly InMemoryOutbox _outbox;
        private readonly RequestContext _contextA;
        private readonly RequestContext _contextB;
        private readonly string _firstMessageWithCausationA;
        private readonly string _secondMessageWithCausationA;
        private readonly string _messageWithCausationB;

        public InMemoryOutboxCausationTrackingTests()
        {
            _outbox = new InMemoryOutbox(_timeProvider) { Tracer = new BrighterTracer() };

            _contextA = new RequestContext();
            _contextA.Bag[RequestContextBagNames.CausationId] = CausationA;
            _contextB = new RequestContext();
            _contextB.Bag[RequestContextBagNames.CausationId] = CausationB;

            _firstMessageWithCausationA = Guid.NewGuid().ToString();
            _secondMessageWithCausationA = Guid.NewGuid().ToString();
            _messageWithCausationB = Guid.NewGuid().ToString();

            _outbox.Add(CreateMessage(_firstMessageWithCausationA), _contextA);
            _outbox.Add(CreateMessage(_secondMessageWithCausationA), _contextA);
            _outbox.Add(CreateMessage(_messageWithCausationB), _contextB);

            //all three start dispatched
            var dispatchedAt = _timeProvider.GetUtcNow();
            _outbox.MarkDispatched(_firstMessageWithCausationA, _contextA, dispatchedAt);
            _outbox.MarkDispatched(_secondMessageWithCausationA, _contextA, dispatchedAt);
            _outbox.MarkDispatched(_messageWithCausationB, _contextB, dispatchedAt);

            _timeProvider.Advance(TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void When_replaying_causation_on_outbox_should_clear_dispatch_state()
        {
            //Act
            ((IAmACausationTrackingOutbox)_outbox).ReplayCausation(CausationA, _contextA);

            //Assert — the two CausationA messages are outstanding again
            var outstanding = _outbox.OutstandingMessages(TimeSpan.Zero, _contextA).Select(m => m.Id.Value).ToArray();
            Assert.Contains(_firstMessageWithCausationA, outstanding);
            Assert.Contains(_secondMessageWithCausationA, outstanding);

            //Assert — the CausationB message is untouched and still dispatched
            var dispatched = _outbox.DispatchedMessages(TimeSpan.FromSeconds(5), _contextB).Select(m => m.Id.Value).ToArray();
            Assert.Contains(_messageWithCausationB, dispatched);
            Assert.DoesNotContain(_messageWithCausationB, outstanding);
        }

        [Fact]
        public async Task When_replaying_causation_on_outbox_should_clear_dispatch_state_async()
        {
            //Act
            await ((IAmACausationTrackingOutbox)_outbox).ReplayCausationAsync(CausationA, _contextA);

            //Assert — the two CausationA messages are outstanding again
            var outstanding = _outbox.OutstandingMessages(TimeSpan.Zero, _contextA).Select(m => m.Id.Value).ToArray();
            Assert.Contains(_firstMessageWithCausationA, outstanding);
            Assert.Contains(_secondMessageWithCausationA, outstanding);

            //Assert — the CausationB message is untouched and still dispatched
            var dispatched = _outbox.DispatchedMessages(TimeSpan.FromSeconds(5), _contextB).Select(m => m.Id.Value).ToArray();
            Assert.Contains(_messageWithCausationB, dispatched);
            Assert.DoesNotContain(_messageWithCausationB, outstanding);
        }

        [Fact]
        public void When_asking_in_memory_outbox_if_it_supports_causation_tracking_should_be_true()
        {
            //Act
            var supportsCausationTracking = ((IAmACausationTrackingOutbox)_outbox).SupportsCausationTracking();

            //Assert
            Assert.True(supportsCausationTracking);
        }

        private static Message CreateMessage(string id)
            => new Message(
                new MessageHeader(id, new RoutingKey("test_topic"), MessageType.MT_DOCUMENT),
                new MessageBody("message body"));
    }
}
