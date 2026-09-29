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

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline;

public class CommandProcessorGlobalInboxScopeTests
{
    [Test]
    [Arguments(InboxScope.Commands, true, false)]
    [Arguments(InboxScope.Events, false, true)]
    [Arguments(InboxScope.All, true, true)]
    [Arguments(null, false, false)]
    public async System.Threading.Tasks.Task When_dispatching_duplicate_requests_should_apply_the_global_inbox_scope(
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
        Exception? commandError = null;
        try
        {
            processor.Send(duplicateCommand);
        }
        catch (Exception e)
        {
            commandError = e;
        }
        Exception? eventError = null;
        try
        {
            processor.Publish(duplicateEvent);
        }
        catch (Exception e)
        {
            eventError = e;
        }

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
        await Assert.That(inbox.Exists<InboxScopeCommand>(command.Id, typeof(InboxScopeCommandHandler).FullName!, null)).IsEqualTo(shouldInboxCommands);
        await Assert.That(inbox.Exists<InboxScopeEvent>(@event.Id, typeof(InboxScopeEventHandler).FullName!, null)).IsEqualTo(shouldInboxEvents);
    }
}
