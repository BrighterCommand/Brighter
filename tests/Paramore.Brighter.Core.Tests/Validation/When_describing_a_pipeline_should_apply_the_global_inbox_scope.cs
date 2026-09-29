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
using Paramore.Brighter.Core.Tests.Validation.TestDoubles;

namespace Paramore.Brighter.Core.Tests.Validation;

public class GlobalInboxScopeDescriptionTests
{
    [Test]
    [Arguments(typeof(InboxScopeCommand), typeof(InboxScopeCommandHandler), InboxScope.Commands, true)]
    [Arguments(typeof(InboxScopeCommand), typeof(InboxScopeCommandHandler), InboxScope.Events, false)]
    [Arguments(typeof(InboxScopeCommand), typeof(InboxScopeCommandHandler), InboxScope.All, true)]
    [Arguments(typeof(InboxScopeAsyncCommand), typeof(InboxScopeAsyncCommandHandler), InboxScope.Commands, true)]
    [Arguments(typeof(InboxScopeAsyncCommand), typeof(InboxScopeAsyncCommandHandler), InboxScope.Events, false)]
    [Arguments(typeof(InboxScopeAsyncCommand), typeof(InboxScopeAsyncCommandHandler), InboxScope.All, true)]
    [Arguments(typeof(InboxScopeEvent), typeof(InboxScopeEventHandler), InboxScope.Commands, false)]
    [Arguments(typeof(InboxScopeEvent), typeof(InboxScopeEventHandler), InboxScope.Events, true)]
    [Arguments(typeof(InboxScopeEvent), typeof(InboxScopeEventHandler), InboxScope.All, true)]
    [Arguments(typeof(InboxScopeAsyncEvent), typeof(InboxScopeAsyncEventHandler), InboxScope.Commands, false)]
    [Arguments(typeof(InboxScopeAsyncEvent), typeof(InboxScopeAsyncEventHandler), InboxScope.Events, true)]
    [Arguments(typeof(InboxScopeAsyncEvent), typeof(InboxScopeAsyncEventHandler), InboxScope.All, true)]
    [Arguments(typeof(MyBareRequest), typeof(MyValidationHandler<MyBareRequest>), InboxScope.Commands, true)]
    [Arguments(typeof(MyBareRequest), typeof(MyValidationHandler<MyBareRequest>), InboxScope.Events, true)]
    [Arguments(typeof(MyBareRequest), typeof(MyValidationHandler<MyBareRequest>), InboxScope.All, true)]
    [Arguments(typeof(MyBareRequest), typeof(MyValidationHandlerAsync<MyBareRequest>), InboxScope.Commands, true)]
    [Arguments(typeof(MyBareRequest), typeof(MyValidationHandlerAsync<MyBareRequest>), InboxScope.Events, true)]
    [Arguments(typeof(MyBareRequest), typeof(MyValidationHandlerAsync<MyBareRequest>), InboxScope.All, true)]
    public async System.Threading.Tasks.Task When_describing_a_pipeline_should_apply_the_global_inbox_scope(
        Type requestType, Type handlerType, InboxScope scope, bool shouldUseInbox)
    {
        // Arrange
        var registry = new SubscriberRegistry();
        registry.Add(requestType, handlerType);
        using var builder = new PipelineBuilder<IRequest>(registry, new InboxConfiguration(scope: scope));

        // Act
        var description = await Assert.That(builder.Describe(requestType)).HasSingleItem();

        // Assert
        if (shouldUseInbox)
            await Assert.That(description.BeforeSteps).HasSingleItem();
        else
            await Assert.That(description.BeforeSteps).IsEmpty();
    }
}
