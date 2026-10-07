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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Inbox;
using Paramore.Brighter.Inbox.Handlers;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.OnceOnly
{
    public class UseInboxHandlerReplayTelemetryTests
    {
        private const string ReplayEventName = "UseInboxHandler Duplicate Replay";
        private const string ReplaySkippedEventName = "UseInboxHandler Duplicate Replay Skipped";
        private const string CausationId = "original-causation-id";
        private const string ContextKey = "UseInboxHandlerReplayTelemetryTests";

        private readonly FakeTimeProvider _timeProvider = new();
        private readonly MyCommand _command;
        private readonly InMemoryInbox _inbox;
        private readonly InMemoryOutbox _outbox;

        public UseInboxHandlerReplayTelemetryTests()
        {
            _inbox = new InMemoryInbox(_timeProvider);
            _outbox = new InMemoryOutbox(_timeProvider) { Tracer = new BrighterTracer() };
            _command = new MyCommand { Value = "My Test String" };

            //Arrange — the command has already been seen, with a known causation id stored in the inbox
            var seedContext = new RequestContext();
            seedContext.Bag[RequestContextBagNames.CausationId] = CausationId;
            _inbox.Add(_command, ContextKey, seedContext);

            //Arrange — the outbox holds a dispatched message for that causation
            var message = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("test_topic"), MessageType.MT_DOCUMENT),
                new MessageBody("message body"));
            _outbox.Add(message, seedContext);
            _outbox.MarkDispatched(message.Id, seedContext, _timeProvider.GetUtcNow());
            _timeProvider.Advance(TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void When_replaying_duplicate_should_add_replay_telemetry_event_to_span()
        {
            //Arrange
            using var span = new Activity("pipeline").Start();
            var handler = new UseInboxHandler<MyCommand>(_inbox, _outbox);
            handler.InitializeFromAttributeParams(true, ContextKey, OnceOnlyAction.Replay);
            handler.Context = new RequestContext { Span = span };

            //Act
            handler.Handle(_command);

            //Assert — a replay telemetry event is added to the pipeline span
            var replayEvent = span.Events.SingleOrDefault(e => e.Name == ReplayEventName);
            Assert.Equal(ReplayEventName, replayEvent.Name);

            //Assert — the event carries the request id and the replayed causation id
            var tags = replayEvent.Tags.ToDictionary(t => t.Key, t => t.Value);
            Assert.Equal(_command.Id.Value, tags[BrighterSemanticConventions.RequestId]);
            Assert.Equal(CausationId, tags[BrighterSemanticConventions.CausationId]);
        }

        [Fact]
        public async Task When_replaying_duplicate_async_should_add_replay_telemetry_event_to_span()
        {
            //Arrange
            using var span = new Activity("pipeline").Start();
            var handler = new UseInboxHandlerAsync<MyCommand>(_inbox, _outbox);
            handler.InitializeFromAttributeParams(true, ContextKey, OnceOnlyAction.Replay);
            handler.Context = new RequestContext { Span = span };

            //Act
            await handler.HandleAsync(_command);

            //Assert — a replay telemetry event is added to the pipeline span
            var replayEvent = span.Events.SingleOrDefault(e => e.Name == ReplayEventName);
            Assert.Equal(ReplayEventName, replayEvent.Name);

            //Assert — the event carries the request id and the replayed causation id
            var tags = replayEvent.Tags.ToDictionary(t => t.Key, t => t.Value);
            Assert.Equal(_command.Id.Value, tags[BrighterSemanticConventions.RequestId]);
            Assert.Equal(CausationId, tags[BrighterSemanticConventions.CausationId]);
        }

        [Fact]
        public void When_replaying_duplicate_with_no_causation_id_should_add_distinct_skipped_event()
        {
            //Arrange — a command that has been seen but has NO causation id stored against it
            var inbox = new InMemoryInbox(_timeProvider);
            var outbox = new InMemoryOutbox(_timeProvider) { Tracer = new BrighterTracer() };
            var command = new MyCommand { Value = "No Causation" };
            inbox.Add(command, ContextKey, new RequestContext());

            using var span = new Activity("pipeline").Start();
            var handler = new UseInboxHandler<MyCommand>(inbox, outbox);
            handler.InitializeFromAttributeParams(true, ContextKey, OnceOnlyAction.Replay);
            handler.Context = new RequestContext { Span = span };

            //Act
            handler.Handle(command);

            //Assert — nothing was replayed, so the event is the distinct "skipped" event, not the replay event
            Assert.DoesNotContain(span.Events, e => e.Name == ReplayEventName);
            var skippedEvent = span.Events.SingleOrDefault(e => e.Name == ReplaySkippedEventName);
            Assert.Equal(ReplaySkippedEventName, skippedEvent.Name);

            //Assert — the event still carries the request id for correlation
            var tags = skippedEvent.Tags.ToDictionary(t => t.Key, t => t.Value);
            Assert.Equal(command.Id.Value, tags[BrighterSemanticConventions.RequestId]);
        }

        [Fact]
        public void When_replaying_duplicate_but_outbox_could_not_replay_should_add_distinct_skipped_event()
        {
            //Arrange — a seen command WITH a causation id, but the outbox cannot replay it because its live
            //schema does not support causation tracking (the "inbox migrated, outbox not" mixed state).
            using var span = new Activity("pipeline").Start();
            var handler = new UseInboxHandler<MyCommand>(_inbox, new MixedMigrationOutbox());
            handler.InitializeFromAttributeParams(true, ContextKey, OnceOnlyAction.Replay);
            handler.Context = new RequestContext { Span = span };

            //Act
            handler.Handle(_command);

            //Assert — nothing was actually replayed, so the event is the "skipped" event, not the replay event
            Assert.DoesNotContain(span.Events, e => e.Name == ReplayEventName);
            var skippedEvent = span.Events.SingleOrDefault(e => e.Name == ReplaySkippedEventName);
            Assert.Equal(ReplaySkippedEventName, skippedEvent.Name);

            //Assert — the event still carries the request id for correlation
            var tags = skippedEvent.Tags.ToDictionary(t => t.Key, t => t.Value);
            Assert.Equal(_command.Id.Value, tags[BrighterSemanticConventions.RequestId]);
        }

        [Fact]
        public async Task When_replaying_duplicate_async_but_outbox_could_not_replay_should_add_distinct_skipped_event()
        {
            //Arrange — a seen command WITH a causation id, but the outbox cannot replay it (mixed-migration state)
            using var span = new Activity("pipeline").Start();
            var handler = new UseInboxHandlerAsync<MyCommand>(_inbox, new MixedMigrationOutbox());
            handler.InitializeFromAttributeParams(true, ContextKey, OnceOnlyAction.Replay);
            handler.Context = new RequestContext { Span = span };

            //Act
            await handler.HandleAsync(_command);

            //Assert — nothing was actually replayed, so the event is the "skipped" event, not the replay event
            Assert.DoesNotContain(span.Events, e => e.Name == ReplayEventName);
            var skippedEvent = span.Events.SingleOrDefault(e => e.Name == ReplaySkippedEventName);
            Assert.Equal(ReplaySkippedEventName, skippedEvent.Name);

            //Assert — the event still carries the request id for correlation
            var tags = skippedEvent.Tags.ToDictionary(t => t.Key, t => t.Value);
            Assert.Equal(_command.Id.Value, tags[BrighterSemanticConventions.RequestId]);
        }

        [Fact]
        public void When_replaying_duplicate_without_brighter_instrumentation_should_not_add_replay_event()
        {
            //Arrange — a context whose instrumentation does not include the Brighter flag
            using var span = new Activity("pipeline").Start();
            var handler = new UseInboxHandler<MyCommand>(_inbox, _outbox);
            handler.InitializeFromAttributeParams(true, ContextKey, OnceOnlyAction.Replay);
            handler.Context = new RequestContext
            {
                Span = span, InstrumentationOptions = InstrumentationOptions.RequestInformation
            };

            //Act
            handler.Handle(_command);

            //Assert — no telemetry event is written when Brighter instrumentation is disabled
            Assert.DoesNotContain(span.Events, e => e.Name == ReplayEventName);
        }

        [Fact]
        public void When_replaying_duplicate_with_no_span_should_not_throw_and_still_replay()
        {
            //Arrange — no span on the context
            var handler = new UseInboxHandler<MyCommand>(_inbox, _outbox);
            handler.InitializeFromAttributeParams(true, ContextKey, OnceOnlyAction.Replay);
            var context = new RequestContext();
            handler.Context = context;

            //Act — replaying without a span must be a safe no-op for telemetry
            handler.Handle(_command);

            //Assert — the matching causation's message is outstanding again (replay still happened)
            var outstanding = _outbox.OutstandingMessages(TimeSpan.Zero, context);
            Assert.Single(outstanding);
        }

        // A causation-tracking outbox whose live schema does NOT support causation tracking, so ReplayCausation
        // is a no-op and reports false. Simulates the "inbox migrated, outbox not" mixed-migration state where
        // the handler is handed a real causation id but the outbox resends nothing.
        private sealed class MixedMigrationOutbox : IAmACausationTrackingOutbox
        {
            public bool SupportsCausationTracking() => false;

            public Task<bool> SupportsCausationTrackingAsync(CancellationToken cancellationToken = default)
                => Task.FromResult(false);

            public bool ReplayCausation(string causationId, RequestContext? requestContext,
                Dictionary<string, object>? args = null)
                => false;

            public Task<bool> ReplayCausationAsync(string causationId, RequestContext? requestContext,
                Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
                => Task.FromResult(false);
        }
    }
}
