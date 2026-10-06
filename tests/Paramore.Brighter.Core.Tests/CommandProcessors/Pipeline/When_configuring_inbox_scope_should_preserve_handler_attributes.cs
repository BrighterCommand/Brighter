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
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Inbox.Handlers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline;

public class GlobalInboxScopeAttributeTests
{
    [Theory]
    [InlineData(InboxScope.All, true)]
    [InlineData(InboxScope.Events, true)]
    [InlineData(InboxScope.All, false)]
    [InlineData(InboxScope.Events, false)]
    public void When_configuring_inbox_scope_should_preserve_handler_attributes(InboxScope scope, bool useExplicitInbox)
    {
        // Arrange
        var inbox = new InMemoryInbox(TimeProvider.System);
        var registry = new SubscriberRegistry();
        var handlerType = useExplicitInbox ? typeof(MyCommandInboxedHandler) : typeof(MyNoInboxCommandHandler);
        registry.Add(typeof(MyCommand), handlerType);
        var factory = new SimpleHandlerFactorySync(type =>
            type == typeof(UseInboxHandler<MyCommand>)
                ? new UseInboxHandler<MyCommand>(inbox)
                : useExplicitInbox ? new MyCommandInboxedHandler() : new MyNoInboxCommandHandler());
        var configuration = new InboxConfiguration(inbox, scope, context: _ => "global-inbox");
        using var builder = new PipelineBuilder<MyCommand>(registry, factory, configuration);
        using var describer = new PipelineBuilder<IRequest>(registry, configuration);
        var request = new MyCommand();
        var pipeline = builder.Build(request, new RequestContext()).Single();

        // Act
        pipeline.Handle(request);
        pipeline.Handle(request);
        var description = Assert.Single(describer.Describe(typeof(MyCommand)));

        // Assert
        Assert.Equal(useExplicitInbox,
            inbox.Exists<MyCommand>(request.Id, handlerType.FullName!, null));
        Assert.False(inbox.Exists<MyCommand>(request.Id, "global-inbox", null));
        if (useExplicitInbox)
            Assert.Single(description.BeforeSteps);
        else
            Assert.Empty(description.BeforeSteps);
    }
}
