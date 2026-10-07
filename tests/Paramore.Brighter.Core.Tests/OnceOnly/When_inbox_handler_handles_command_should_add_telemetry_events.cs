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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Inbox;
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.Inbox.Handlers;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.OnceOnly
{
    public class UseInboxHandlerTelemetryTests
    {
        private const string AddEventName = "UseInboxHandler Add";
        private const string ThrowEventName = "UseInboxHandler Duplicate Throw";
        private const string WarnEventName = "UseInboxHandler Duplicate Warn";
        private const string ContextKey = "UseInboxHandlerTelemetryTests";

        private readonly FakeTimeProvider _timeProvider = new();
        private readonly MyCommand _command;
        private readonly InMemoryInbox _inbox;

        public UseInboxHandlerTelemetryTests()
        {
            _inbox = new InMemoryInbox(_timeProvider);
            _command = new MyCommand { Value = "My Test String" };
        }

        private void SeedAsAlreadySeen()
        {
            _inbox.Add(_command, ContextKey, new RequestContext());
        }

        [Fact]
        public void When_inbox_handler_handles_command_should_add_telemetry_events()
        {
            //Arrange — first time the command is seen, so it is added to the inbox
            using var span = new Activity("pipeline").Start();
            var handler = new UseInboxHandler<MyCommand>(_inbox);
            handler.InitializeFromAttributeParams(true, ContextKey, OnceOnlyAction.Throw);
            handler.Context = new RequestContext { Span = span };

            //Act
            handler.Handle(_command);

            //Assert — an Add telemetry event is added to the pipeline span carrying the request id
            var addEvent = span.Events.SingleOrDefault(e => e.Name == AddEventName);
            Assert.Equal(AddEventName, addEvent.Name);
            var tags = addEvent.Tags.ToDictionary(t => t.Key, t => t.Value);
            Assert.Equal(_command.Id.Value, tags[BrighterSemanticConventions.RequestId]);
        }

        [Fact]
        public async Task When_inbox_handler_handles_command_async_should_add_add_telemetry_event()
        {
            //Arrange — first time the command is seen
            using var span = new Activity("pipeline").Start();
            var handler = new UseInboxHandlerAsync<MyCommand>(_inbox);
            handler.InitializeFromAttributeParams(true, ContextKey, OnceOnlyAction.Throw);
            handler.Context = new RequestContext { Span = span };

            //Act
            await handler.HandleAsync(_command);

            //Assert
            var addEvent = span.Events.SingleOrDefault(e => e.Name == AddEventName);
            Assert.Equal(AddEventName, addEvent.Name);
        }

        [Fact]
        public void When_duplicate_with_throw_should_add_throw_telemetry_event()
        {
            //Arrange — the command has already been seen and the action is Throw
            SeedAsAlreadySeen();
            using var span = new Activity("pipeline").Start();
            var handler = new UseInboxHandler<MyCommand>(_inbox);
            handler.InitializeFromAttributeParams(true, ContextKey, OnceOnlyAction.Throw);
            handler.Context = new RequestContext { Span = span };

            //Act — the duplicate throws, but the telemetry event must be written first
            Assert.Throws<OnceOnlyException>(() => handler.Handle(_command));

            //Assert
            var throwEvent = span.Events.SingleOrDefault(e => e.Name == ThrowEventName);
            Assert.Equal(ThrowEventName, throwEvent.Name);
            var tags = throwEvent.Tags.ToDictionary(t => t.Key, t => t.Value);
            Assert.Equal(_command.Id.Value, tags[BrighterSemanticConventions.RequestId]);
        }

        [Fact]
        public async Task When_duplicate_with_throw_async_should_add_throw_telemetry_event()
        {
            //Arrange
            SeedAsAlreadySeen();
            using var span = new Activity("pipeline").Start();
            var handler = new UseInboxHandlerAsync<MyCommand>(_inbox);
            handler.InitializeFromAttributeParams(true, ContextKey, OnceOnlyAction.Throw);
            handler.Context = new RequestContext { Span = span };

            //Act
            await Assert.ThrowsAsync<OnceOnlyException>(() => handler.HandleAsync(_command));

            //Assert
            var throwEvent = span.Events.SingleOrDefault(e => e.Name == ThrowEventName);
            Assert.Equal(ThrowEventName, throwEvent.Name);
        }

        [Fact]
        public void When_duplicate_with_warn_should_add_warn_telemetry_event()
        {
            //Arrange — the command has already been seen and the action is Warn
            SeedAsAlreadySeen();
            using var span = new Activity("pipeline").Start();
            var handler = new UseInboxHandler<MyCommand>(_inbox);
            handler.InitializeFromAttributeParams(true, ContextKey, OnceOnlyAction.Warn);
            handler.Context = new RequestContext { Span = span };

            //Act
            handler.Handle(_command);

            //Assert
            var warnEvent = span.Events.SingleOrDefault(e => e.Name == WarnEventName);
            Assert.Equal(WarnEventName, warnEvent.Name);
            var tags = warnEvent.Tags.ToDictionary(t => t.Key, t => t.Value);
            Assert.Equal(_command.Id.Value, tags[BrighterSemanticConventions.RequestId]);
        }

        [Fact]
        public async Task When_duplicate_with_warn_async_should_add_warn_telemetry_event()
        {
            //Arrange
            SeedAsAlreadySeen();
            using var span = new Activity("pipeline").Start();
            var handler = new UseInboxHandlerAsync<MyCommand>(_inbox);
            handler.InitializeFromAttributeParams(true, ContextKey, OnceOnlyAction.Warn);
            handler.Context = new RequestContext { Span = span };

            //Act
            await handler.HandleAsync(_command);

            //Assert
            var warnEvent = span.Events.SingleOrDefault(e => e.Name == WarnEventName);
            Assert.Equal(WarnEventName, warnEvent.Name);
        }

        [Fact]
        public void When_handling_command_without_brighter_instrumentation_should_not_add_event()
        {
            //Arrange — a context whose instrumentation does not include the Brighter flag
            using var span = new Activity("pipeline").Start();
            var handler = new UseInboxHandler<MyCommand>(_inbox);
            handler.InitializeFromAttributeParams(true, ContextKey, OnceOnlyAction.Throw);
            handler.Context = new RequestContext
            {
                Span = span, InstrumentationOptions = InstrumentationOptions.RequestInformation
            };

            //Act
            handler.Handle(_command);

            //Assert — no telemetry event is written when Brighter instrumentation is disabled
            Assert.DoesNotContain(span.Events, e => e.Name == AddEventName);
        }

        [Fact]
        public void When_handling_command_with_no_span_should_not_throw_and_still_add()
        {
            //Arrange — no span on the context
            var handler = new UseInboxHandler<MyCommand>(_inbox);
            handler.InitializeFromAttributeParams(true, ContextKey, OnceOnlyAction.Throw);
            var context = new RequestContext();
            handler.Context = context;

            //Act — handling without a span must be a safe no-op for telemetry
            handler.Handle(_command);

            //Assert — the command was still recorded in the inbox (handling still happened)
            Assert.True(_inbox.Exists<MyCommand>(_command.Id.Value, ContextKey, context));
        }
    }
}
