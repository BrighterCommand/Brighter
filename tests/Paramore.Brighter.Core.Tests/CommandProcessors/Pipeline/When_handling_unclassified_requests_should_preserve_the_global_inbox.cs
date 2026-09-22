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
using Paramore.Brighter.Core.Tests.Validation.TestDoubles;
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.Inbox.Handlers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline;

public class GlobalInboxScopeUnclassifiedRequestTests
{
    [Theory]
    [InlineData(InboxScope.Commands)]
    [InlineData(InboxScope.Events)]
    [InlineData(InboxScope.All)]
    public void When_handling_unclassified_requests_should_preserve_the_global_inbox(InboxScope scope)
    {
        // Arrange
        var inbox = new InMemoryInbox(TimeProvider.System);
        var registry = new SubscriberRegistry();
        registry.Register<MyBareRequest, MyValidationHandler<MyBareRequest>>();
        var factory = new SimpleHandlerFactorySync(type =>
            type == typeof(UseInboxHandler<MyBareRequest>)
                ? new UseInboxHandler<MyBareRequest>(inbox)
                : new MyValidationHandler<MyBareRequest>());
        using var builder = new PipelineBuilder<MyBareRequest>(registry, factory,
            new InboxConfiguration(inbox, scope));
        var request = new MyBareRequest();
        var pipeline = builder.Build(request, new RequestContext()).Single();

        // Act
        pipeline.Handle(request);

        // Assert
        Assert.True(inbox.Exists<MyBareRequest>(
            request.Id, typeof(MyValidationHandler<MyBareRequest>).FullName!, null));
        Assert.Throws<OnceOnlyException>(() => pipeline.Handle(request));
    }
}
