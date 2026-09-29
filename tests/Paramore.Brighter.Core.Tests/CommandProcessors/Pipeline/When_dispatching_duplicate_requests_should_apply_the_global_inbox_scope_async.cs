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

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline;

public class CommandProcessorGlobalInboxScopeAsyncTests
{
    [Test]
    [Arguments(InboxScope.Commands, true, false)]
    [Arguments(InboxScope.Events, false, true)]
    [Arguments(InboxScope.All, true, true)]
    [Arguments(null, false, false)]
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
        var commandError = await TestExceptionRecorder.CaptureAsync(() => processor.SendAsync(duplicateCommand));
        var eventError = await TestExceptionRecorder.CaptureAsync(() => processor.PublishAsync(duplicateEvent));

        // Assert
        if (shouldInboxCommands)
            await Assert.That(commandError).IsTypeOf<OnceOnlyException>();
        else
            await Assert.That(commandError).IsNull();

        if (shouldInboxEvents)
        {
            var aggregate = await Assert.That(eventError).IsTypeOf<AggregateException>();
            await Assert.That((await Assert.That(aggregate.InnerExceptions).HasSingleItem())).IsTypeOf<OnceOnlyException>();
        }
        else
            await Assert.That(eventError).IsNull();

        await Assert.That(command.HandleCount).IsEqualTo(1);
        await Assert.That(@event.HandleCount).IsEqualTo(1);
        await Assert.That(duplicateCommand.HandleCount).IsEqualTo(shouldInboxCommands ? 0 : 1);
        await Assert.That(duplicateEvent.HandleCount).IsEqualTo(shouldInboxEvents ? 0 : 1);
        await Assert.That(await inbox.ExistsAsync<InboxScopeAsyncCommand>(command.Id, typeof(InboxScopeAsyncCommandHandler).FullName!, null)).IsEqualTo(shouldInboxCommands);
        await Assert.That(await inbox.ExistsAsync<InboxScopeAsyncEvent>(@event.Id, typeof(InboxScopeAsyncEventHandler).FullName!, null)).IsEqualTo(shouldInboxEvents);
    }
}
