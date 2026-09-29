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


namespace Paramore.Brighter.Extensions.Tests;

public class CommandProcessorBuilderInboxTests
{
    [Test]
    [Arguments(InboxScope.Commands, true)]
    [Arguments(InboxScope.Commands, false)]
    [Arguments(InboxScope.Events, true)]
    [Arguments(InboxScope.Events, false)]
    [Arguments(InboxScope.All, true)]
    [Arguments(InboxScope.All, false)]
    public async System.Threading.Tasks.Task When_building_without_external_bus_should_apply_the_inbox_configuration(
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
        Exception? exception = null;
        try
        {
            processor.Send(duplicate);
        }
        catch (Exception e)
        {
            exception = e;
        }

        // Assert
        if (shouldReject)
            await Assert.That(exception).IsTypeOf<OnceOnlyException>();
        else
            await Assert.That(exception).IsNull();
        await Assert.That(command.HandleCount).IsEqualTo(1);
        await Assert.That(duplicate.HandleCount).IsEqualTo(shouldReject ? 0 : 1);
        await Assert.That(inbox.Exists<ConsumerGlobalInboxCommand>(command.Id, "builder-inbox", null)).IsEqualTo(shouldStore);
    }
}
