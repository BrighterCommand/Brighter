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
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Inbox;
using Paramore.Brighter.Inbox.Handlers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline;

public class GlobalInboxScopeAsyncEventTests
{
    [Theory]
    [InlineData(InboxScope.Commands, false)]
    [InlineData(InboxScope.Events, true)]
    [InlineData(InboxScope.All, true)]
    public async Task When_handling_events_should_apply_the_global_inbox_scope_async(InboxScope scope, bool shouldUseInbox)
    {
        // Arrange
        var inbox = new InMemoryInbox(TimeProvider.System);
        var registry = new SubscriberRegistry();
        registry.RegisterAsync<InboxScopeAsyncEvent, InboxScopeAsyncEventHandler>();
        var factory = new SimpleHandlerFactoryAsync(type =>
            type == typeof(UseInboxHandlerAsync<InboxScopeAsyncEvent>)
                ? new UseInboxHandlerAsync<InboxScopeAsyncEvent>(inbox)
                : new InboxScopeAsyncEventHandler());
        using var builder = new PipelineBuilder<InboxScopeAsyncEvent>(registry, factory,
            new InboxConfiguration(inbox, scope, actionOnExists: OnceOnlyAction.Warn));
        var request = new InboxScopeAsyncEvent();
        var pipeline = builder.BuildAsync(request, new RequestContext(), false).Single();

        // Act
        await pipeline.HandleAsync(request);
        await pipeline.HandleAsync(request);

        // Assert
        Assert.Equal(shouldUseInbox ? 1 : 2, request.HandleCount);
        Assert.Equal(shouldUseInbox,
            await inbox.ExistsAsync<InboxScopeAsyncEvent>(request.Id, typeof(InboxScopeAsyncEventHandler).FullName!, null));
    }
}
