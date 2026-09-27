#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.Inbox.Handlers;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline;

public class CommandProcessorGlobalInboxScopeTests
{
    [Theory]
    [InlineData(InboxScope.Commands, true, false)]
    [InlineData(InboxScope.Events, false, true)]
    [InlineData(InboxScope.All, true, true)]
    [InlineData(null, false, false)]
    public void When_dispatching_duplicate_requests_should_apply_the_global_inbox_scope(
        InboxScope? scope, bool shouldInboxCommands, bool shouldInboxEvents)
    {
        // Arrange
        var inbox = new InMemoryInbox(TimeProvider.System);
        var registry = new SubscriberRegistry();
        registry.Register<InboxScopeCommand, InboxScopeCommandHandler>();
        registry.Register<InboxScopeEvent, InboxScopeEventHandler>();
        var factory = new SimpleHandlerFactorySync(type => type switch
        {
            _ when type == typeof(InboxScopeCommandHandler) => new InboxScopeCommandHandler(),
            _ when type == typeof(InboxScopeEventHandler) => new InboxScopeEventHandler(),
            _ when type == typeof(UseInboxHandler<InboxScopeCommand>) => new UseInboxHandler<InboxScopeCommand>(inbox),
            _ when type == typeof(UseInboxHandler<InboxScopeEvent>) => new UseInboxHandler<InboxScopeEvent>(inbox),
            _ => throw new InvalidOperationException($"Unexpected handler type {type}")
        });
        var configuration = scope.HasValue ? new InboxConfiguration(inbox, scope.Value) : null;
        var processor = new CommandProcessor(registry, factory, new InMemoryRequestContextFactory(),
            new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory(),
            inboxConfiguration: configuration);
        var command = new InboxScopeCommand();
        var duplicateCommand = new InboxScopeCommand { Id = command.Id };
        var @event = new InboxScopeEvent();
        var duplicateEvent = new InboxScopeEvent { Id = @event.Id };

        // Act
        processor.Send(command);
        processor.Publish(@event);
        var commandError = Record.Exception(() => processor.Send(duplicateCommand));
        var eventError = Record.Exception(() => processor.Publish(duplicateEvent));

        // Assert
        if (shouldInboxCommands)
            Assert.IsType<OnceOnlyException>(commandError);
        else
            Assert.Null(commandError);

        if (shouldInboxEvents)
        {
            var aggregate = Assert.IsType<AggregateException>(eventError);
            Assert.IsType<OnceOnlyException>(Assert.Single(aggregate.InnerExceptions));
        }
        else
            Assert.Null(eventError);

        Assert.Equal(1, command.HandleCount);
        Assert.Equal(1, @event.HandleCount);
        Assert.Equal(shouldInboxCommands ? 0 : 1, duplicateCommand.HandleCount);
        Assert.Equal(shouldInboxEvents ? 0 : 1, duplicateEvent.HandleCount);
        Assert.Equal(shouldInboxCommands,
            inbox.Exists<InboxScopeCommand>(command.Id, typeof(InboxScopeCommandHandler).FullName!, null));
        Assert.Equal(shouldInboxEvents,
            inbox.Exists<InboxScopeEvent>(@event.Id, typeof(InboxScopeEventHandler).FullName!, null));
    }
}
