#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.Inbox;
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.Inbox.Handlers;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class CommandProcessorBuilderInboxTests
{
    [Theory]
    [InlineData(InboxScope.Commands, true)]
    [InlineData(InboxScope.Commands, false)]
    [InlineData(InboxScope.Events, true)]
    [InlineData(InboxScope.Events, false)]
    [InlineData(InboxScope.All, true)]
    [InlineData(InboxScope.All, false)]
    public void When_building_without_external_bus_should_apply_the_inbox_configuration(
        InboxScope scope, bool onceOnly)
    {
        // Arrange
        var inbox = new InMemoryInbox(TimeProvider.System);
        var configuration = new InboxConfiguration(inbox, scope, onceOnly, context: _ => "builder-inbox");
        var registry = new SubscriberRegistry();
        registry.Register<ConsumerGlobalInboxCommand, ConsumerGlobalInboxCommandHandler>();
        var factory = new SimpleHandlerFactorySync(type =>
            type == typeof(UseInboxHandler<ConsumerGlobalInboxCommand>)
                ? new UseInboxHandler<ConsumerGlobalInboxCommand>(inbox)
                : new ConsumerGlobalInboxCommandHandler());
        var processor = CommandProcessorBuilder.StartNew(configuration)
            .Handlers(new HandlerConfiguration(registry, factory))
            .DefaultResilience()
            .NoExternalBus()
            .NoInstrumentation()
            .RequestContextFactory(new InMemoryRequestContextFactory())
            .RequestSchedulerFactory(new InMemorySchedulerFactory())
            .Build();
        var command = new ConsumerGlobalInboxCommand();
        var duplicate = new ConsumerGlobalInboxCommand { Id = command.Id };
        var shouldStore = scope != InboxScope.Events;
        var shouldReject = shouldStore && onceOnly;

        // Act
        processor.Send(command);
        var exception = Record.Exception(() => processor.Send(duplicate));

        // Assert
        if (shouldReject)
            Assert.IsType<OnceOnlyException>(exception);
        else
            Assert.Null(exception);
        Assert.Equal(1, command.HandleCount);
        Assert.Equal(shouldReject ? 0 : 1, duplicate.HandleCount);
        Assert.Equal(shouldStore, inbox.Exists<ConsumerGlobalInboxCommand>(command.Id, "builder-inbox", null));
    }
}
