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
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.Inbox.Handlers;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline;

public class CommandProcessorGlobalInboxScopeAsyncTests
{
    [Theory]
    [InlineData(InboxScope.Commands, true, false)]
    [InlineData(InboxScope.Events, false, true)]
    [InlineData(InboxScope.All, true, true)]
    [InlineData(null, false, false)]
    public async Task When_dispatching_duplicate_requests_should_apply_the_global_inbox_scope_async(
        InboxScope? scope, bool shouldInboxCommands, bool shouldInboxEvents)
    {
        // Arrange
        var inbox = new InMemoryInbox(TimeProvider.System);
        var registry = new SubscriberRegistry();
        registry.RegisterAsync<InboxScopeAsyncCommand, InboxScopeAsyncCommandHandler>();
        registry.RegisterAsync<InboxScopeAsyncEvent, InboxScopeAsyncEventHandler>();
        var factory = new SimpleHandlerFactoryAsync(type => type switch
        {
            _ when type == typeof(InboxScopeAsyncCommandHandler) => new InboxScopeAsyncCommandHandler(),
            _ when type == typeof(InboxScopeAsyncEventHandler) => new InboxScopeAsyncEventHandler(),
            _ when type == typeof(UseInboxHandlerAsync<InboxScopeAsyncCommand>) => new UseInboxHandlerAsync<InboxScopeAsyncCommand>(inbox),
            _ when type == typeof(UseInboxHandlerAsync<InboxScopeAsyncEvent>) => new UseInboxHandlerAsync<InboxScopeAsyncEvent>(inbox),
            _ => throw new InvalidOperationException($"Unexpected handler type {type}")
        });
        var configuration = scope.HasValue ? new InboxConfiguration(inbox, scope.Value) : null;
        var processor = new CommandProcessor(registry, factory, new InMemoryRequestContextFactory(),
            new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory(),
            inboxConfiguration: configuration);
        var command = new InboxScopeAsyncCommand();
        var duplicateCommand = new InboxScopeAsyncCommand { Id = command.Id };
        var @event = new InboxScopeAsyncEvent();
        var duplicateEvent = new InboxScopeAsyncEvent { Id = @event.Id };

        // Act
        await processor.SendAsync(command);
        await processor.PublishAsync(@event);
        var commandError = await Record.ExceptionAsync(() => processor.SendAsync(duplicateCommand));
        var eventError = await Record.ExceptionAsync(() => processor.PublishAsync(duplicateEvent));

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
            await inbox.ExistsAsync<InboxScopeAsyncCommand>(command.Id, typeof(InboxScopeAsyncCommandHandler).FullName!, null));
        Assert.Equal(shouldInboxEvents,
            await inbox.ExistsAsync<InboxScopeAsyncEvent>(@event.Id, typeof(InboxScopeAsyncEventHandler).FullName!, null));
    }
}
