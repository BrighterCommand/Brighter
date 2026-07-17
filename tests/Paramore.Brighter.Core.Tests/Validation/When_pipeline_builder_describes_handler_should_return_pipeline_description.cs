#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Linq;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Validation;

namespace Paramore.Brighter.Core.Tests.Validation;
public class PipelineBuilderDescribeTests
{
    [Test]
    public async Task When_describing_sync_handler_should_return_description_with_request_and_handler_types()
    {
        // Arrange
        var registry = new SubscriberRegistry();
        registry.Add(typeof(MyCommand), typeof(MyPreAndPostDecoratedHandler));
        var pipelineBuilder = new PipelineBuilder<MyCommand>(registry);
        // Act
        var descriptions = pipelineBuilder.Describe(typeof(MyCommand)).ToList();
        // Assert
        await Assert.That(descriptions).HasSingleItem();
        var description = descriptions[0];
        await Assert.That(description.RequestType).IsEqualTo(typeof(MyCommand));
        await Assert.That(description.HandlerType).IsEqualTo(typeof(MyPreAndPostDecoratedHandler));
        await Assert.That(description.IsAsync).IsFalse();
    }

    [Test]
    public async Task When_describing_sync_handler_should_list_before_steps_in_step_order()
    {
        // Arrange — MyPreAndPostDecoratedHandler has [MyPreValidationHandler(2, Before)]
        var registry = new SubscriberRegistry();
        registry.Add(typeof(MyCommand), typeof(MyPreAndPostDecoratedHandler));
        var pipelineBuilder = new PipelineBuilder<MyCommand>(registry);
        // Act
        var description = pipelineBuilder.Describe(typeof(MyCommand)).First();
        // Assert
        await Assert.That(description.BeforeSteps).HasSingleItem();
        var beforeStep = description.BeforeSteps[0];
        await Assert.That(beforeStep.AttributeType).IsEqualTo(typeof(MyPreValidationHandlerAttribute));
        await Assert.That(beforeStep.HandlerType).IsEqualTo(typeof(MyValidationHandler<>));
        await Assert.That(beforeStep.Step).IsEqualTo(2);
        await Assert.That(beforeStep.Timing).IsEqualTo(HandlerTiming.Before);
    }

    [Test]
    public async Task When_describing_sync_handler_should_list_after_steps()
    {
        // Arrange — MyPreAndPostDecoratedHandler has [MyPostLoggingHandler(1, After)]
        var registry = new SubscriberRegistry();
        registry.Add(typeof(MyCommand), typeof(MyPreAndPostDecoratedHandler));
        var pipelineBuilder = new PipelineBuilder<MyCommand>(registry);
        // Act
        var description = pipelineBuilder.Describe(typeof(MyCommand)).First();
        // Assert
        await Assert.That(description.AfterSteps).HasSingleItem();
        var afterStep = description.AfterSteps[0];
        await Assert.That(afterStep.AttributeType).IsEqualTo(typeof(MyPostLoggingHandlerAttribute));
        await Assert.That(afterStep.HandlerType).IsEqualTo(typeof(MyLoggingHandler<>));
        await Assert.That(afterStep.Step).IsEqualTo(1);
        await Assert.That(afterStep.Timing).IsEqualTo(HandlerTiming.After);
    }

    [Test]
    public async Task When_describing_async_handler_should_set_IsAsync_true()
    {
        // Arrange — MyPreAndPostDecoratedHandlerAsync extends RequestHandlerAsync
        var registry = new SubscriberRegistry();
        registry.Add(typeof(MyCommand), typeof(MyPreAndPostDecoratedHandlerAsync));
        var pipelineBuilder = new PipelineBuilder<MyCommand>(registry);
        // Act
        var description = pipelineBuilder.Describe(typeof(MyCommand)).First();
        // Assert
        await Assert.That(description.IsAsync).IsTrue();
        await Assert.That(description.HandlerType).IsEqualTo(typeof(MyPreAndPostDecoratedHandlerAsync));
    }

    [Test]
    public async Task When_multiple_handlers_registered_should_produce_multiple_descriptions()
    {
        // Arrange — two handler types for the same request type
        var registry = new SubscriberRegistry();
        registry.Add(typeof(MyCommand), typeof(MyPreAndPostDecoratedHandler));
        registry.Add(typeof(MyCommand), typeof(MyPreAndPostDecoratedHandlerAsync));
        var pipelineBuilder = new PipelineBuilder<MyCommand>(registry);
        // Act
        var descriptions = pipelineBuilder.Describe(typeof(MyCommand)).ToList();
        // Assert
        await Assert.That(descriptions.Count).IsEqualTo(2);
        await Assert.That((descriptions).Any(d => d.HandlerType == typeof(MyPreAndPostDecoratedHandler))).IsTrue();
        await Assert.That((descriptions).Any(d => d.HandlerType == typeof(MyPreAndPostDecoratedHandlerAsync))).IsTrue();
    }

    [Test]
    public async Task When_parameterless_describe_should_iterate_all_registered_request_types()
    {
        // Arrange — register handlers for MyCommand only
        var registry = new SubscriberRegistry();
        registry.Add(typeof(MyCommand), typeof(MyPreAndPostDecoratedHandler));
        var pipelineBuilder = new PipelineBuilder<MyCommand>(registry);
        // Act — parameterless Describe() should find all registered request types
        var descriptions = pipelineBuilder.Describe().ToList();
        // Assert
        await Assert.That(descriptions).HasSingleItem();
        await Assert.That(descriptions[0].RequestType).IsEqualTo(typeof(MyCommand));
    }
}
