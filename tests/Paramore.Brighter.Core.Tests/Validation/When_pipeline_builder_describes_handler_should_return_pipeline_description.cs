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
using Xunit;

namespace Paramore.Brighter.Core.Tests.Validation;

public class PipelineBuilderDescribeTests
{
    [Fact]
    public void When_describing_sync_handler_should_return_description_with_request_and_handler_types()
    {
        // Arrange
        var registry = new SubscriberRegistry();
        registry.Add(typeof(MyCommand), typeof(MyPreAndPostDecoratedHandler));

        var pipelineBuilder = new PipelineBuilder<MyCommand>(registry);
        PipelineBuilder<MyCommand>.ClearPipelineCache();

        // Act
        var descriptions = pipelineBuilder.Describe(typeof(MyCommand)).ToList();

        // Assert
        Assert.Single(descriptions);
        var description = descriptions[0];
        Assert.Equal(typeof(MyCommand), description.RequestType);
        Assert.Equal(typeof(MyPreAndPostDecoratedHandler), description.HandlerType);
        Assert.False(description.IsAsync);
    }

    [Fact]
    public void When_describing_sync_handler_should_list_before_steps_in_step_order()
    {
        // Arrange — MyPreAndPostDecoratedHandler has [MyPreValidationHandler(2, Before)]
        var registry = new SubscriberRegistry();
        registry.Add(typeof(MyCommand), typeof(MyPreAndPostDecoratedHandler));

        var pipelineBuilder = new PipelineBuilder<MyCommand>(registry);
        PipelineBuilder<MyCommand>.ClearPipelineCache();

        // Act
        var description = pipelineBuilder.Describe(typeof(MyCommand)).First();

        // Assert
        Assert.Single(description.BeforeSteps);
        var beforeStep = description.BeforeSteps[0];
        Assert.Equal(typeof(MyPreValidationHandlerAttribute), beforeStep.AttributeType);
        Assert.Equal(typeof(MyValidationHandler<>), beforeStep.HandlerType);
        Assert.Equal(2, beforeStep.Step);
        Assert.Equal(HandlerTiming.Before, beforeStep.Timing);
    }

    [Fact]
    public void When_describing_sync_handler_should_list_after_steps()
    {
        // Arrange — MyPreAndPostDecoratedHandler has [MyPostLoggingHandler(1, After)]
        var registry = new SubscriberRegistry();
        registry.Add(typeof(MyCommand), typeof(MyPreAndPostDecoratedHandler));

        var pipelineBuilder = new PipelineBuilder<MyCommand>(registry);
        PipelineBuilder<MyCommand>.ClearPipelineCache();

        // Act
        var description = pipelineBuilder.Describe(typeof(MyCommand)).First();

        // Assert
        Assert.Single(description.AfterSteps);
        var afterStep = description.AfterSteps[0];
        Assert.Equal(typeof(MyPostLoggingHandlerAttribute), afterStep.AttributeType);
        Assert.Equal(typeof(MyLoggingHandler<>), afterStep.HandlerType);
        Assert.Equal(1, afterStep.Step);
        Assert.Equal(HandlerTiming.After, afterStep.Timing);
    }

    [Fact]
    public void When_describing_async_handler_should_set_IsAsync_true()
    {
        // Arrange — MyPreAndPostDecoratedHandlerAsync extends RequestHandlerAsync
        var registry = new SubscriberRegistry();
        registry.Add(typeof(MyCommand), typeof(MyPreAndPostDecoratedHandlerAsync));

        var pipelineBuilder = new PipelineBuilder<MyCommand>(registry);
        PipelineBuilder<MyCommand>.ClearPipelineCache();

        // Act
        var description = pipelineBuilder.Describe(typeof(MyCommand)).First();

        // Assert
        Assert.True(description.IsAsync);
        Assert.Equal(typeof(MyPreAndPostDecoratedHandlerAsync), description.HandlerType);
    }

    [Fact]
    public void When_multiple_handlers_registered_should_produce_multiple_descriptions()
    {
        // Arrange — two handler types for the same request type
        var registry = new SubscriberRegistry();
        registry.Add(typeof(MyCommand), typeof(MyPreAndPostDecoratedHandler));
        registry.Add(typeof(MyCommand), typeof(MyPreAndPostDecoratedHandlerAsync));

        var pipelineBuilder = new PipelineBuilder<MyCommand>(registry);
        PipelineBuilder<MyCommand>.ClearPipelineCache();

        // Act
        var descriptions = pipelineBuilder.Describe(typeof(MyCommand)).ToList();

        // Assert
        Assert.Equal(2, descriptions.Count);
        Assert.Contains(descriptions, d => d.HandlerType == typeof(MyPreAndPostDecoratedHandler));
        Assert.Contains(descriptions, d => d.HandlerType == typeof(MyPreAndPostDecoratedHandlerAsync));
    }

    [Fact]
    public void When_parameterless_describe_should_iterate_all_registered_request_types()
    {
        // Arrange — register handlers for MyCommand only
        var registry = new SubscriberRegistry();
        registry.Add(typeof(MyCommand), typeof(MyPreAndPostDecoratedHandler));

        var pipelineBuilder = new PipelineBuilder<MyCommand>(registry);
        PipelineBuilder<MyCommand>.ClearPipelineCache();

        // Act — parameterless Describe() should find all registered request types
        var descriptions = pipelineBuilder.Describe().ToList();

        // Assert
        Assert.Single(descriptions);
        Assert.Equal(typeof(MyCommand), descriptions[0].RequestType);
    }
}
