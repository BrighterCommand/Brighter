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
using Paramore.Brighter.Core.Tests.Validation.TestDoubles;
using Paramore.Brighter.MessageMappers;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Validation;

public class TransformPipelineBuilderDescribeTests
{
    [Fact]
    public void When_describing_custom_mapper_should_return_mapper_type_and_not_default()
    {
        // Arrange — register an explicit (non-default) mapper for MyDescribableCommand
        var mapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => null!),
            new SimpleMessageMapperFactoryAsync(_ => null!));
        mapperRegistry.Register<MyDescribableCommand, MyDescribableCommandMessageMapper>();

        TransformPipelineBuilder.ClearPipelineCache();

        // Act
        var description = TransformPipelineBuilder.DescribeTransforms(
            mapperRegistry, typeof(MyDescribableCommand));

        // Assert
        Assert.NotNull(description);
        Assert.Equal(typeof(MyDescribableCommandMessageMapper), description.MapperType);
        Assert.False(description.IsDefaultMapper);
    }

    [Fact]
    public void When_describing_transforms_should_list_wrap_transforms_in_step_order()
    {
        // Arrange
        var mapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => null!),
            new SimpleMessageMapperFactoryAsync(_ => null!));
        mapperRegistry.Register<MyDescribableCommand, MyDescribableCommandMessageMapper>();

        TransformPipelineBuilder.ClearPipelineCache();

        // Act
        var description = TransformPipelineBuilder.DescribeTransforms(
            mapperRegistry, typeof(MyDescribableCommand));

        // Assert — MyDescribableCommandMessageMapper has [MyDescribableWrapWith(0)] on MapToMessage
        Assert.NotNull(description);
        Assert.Single(description.WrapTransforms);
        var wrapStep = description.WrapTransforms[0];
        Assert.Equal(typeof(MyDescribableWrapWith), wrapStep.AttributeType);
        Assert.Equal(typeof(MyDescribableTransform), wrapStep.TransformType);
        Assert.Equal(0, wrapStep.Step);
    }

    [Fact]
    public void When_describing_transforms_should_list_unwrap_transforms_in_step_order()
    {
        // Arrange
        var mapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => null!),
            new SimpleMessageMapperFactoryAsync(_ => null!));
        mapperRegistry.Register<MyDescribableCommand, MyDescribableCommandMessageMapper>();

        TransformPipelineBuilder.ClearPipelineCache();

        // Act
        var description = TransformPipelineBuilder.DescribeTransforms(
            mapperRegistry, typeof(MyDescribableCommand));

        // Assert — MyDescribableCommandMessageMapper has [MyDescribableUnwrapWith(0)] on MapToRequest
        Assert.NotNull(description);
        Assert.Single(description.UnwrapTransforms);
        var unwrapStep = description.UnwrapTransforms[0];
        Assert.Equal(typeof(MyDescribableUnwrapWith), unwrapStep.AttributeType);
        Assert.Equal(typeof(MyDescribableTransform), unwrapStep.TransformType);
        Assert.Equal(0, unwrapStep.Step);
    }

    [Fact]
    public void When_describing_default_mapper_should_identify_as_default()
    {
        // Arrange — use an open generic default mapper (no explicit registration for this request type)
        var mapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => null!),
            new SimpleMessageMapperFactoryAsync(_ => null!),
            defaultMessageMapper: typeof(JsonMessageMapper<>));

        TransformPipelineBuilder.ClearPipelineCache();

        // Act
        var description = TransformPipelineBuilder.DescribeTransforms(
            mapperRegistry, typeof(MyDescribableCommand));

        // Assert
        Assert.NotNull(description);
        Assert.True(description.IsDefaultMapper);
        Assert.Equal(typeof(JsonMessageMapper<MyDescribableCommand>), description.MapperType);
    }

    [Fact]
    public void When_describing_vanilla_mapper_should_have_empty_transforms()
    {
        // Arrange — MyVanillaDescribableCommandMessageMapper has no wrap/unwrap attributes
        var mapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => null!),
            new SimpleMessageMapperFactoryAsync(_ => null!));
        mapperRegistry.Register<MyDescribableCommand, MyVanillaDescribableCommandMessageMapper>();

        TransformPipelineBuilder.ClearPipelineCache();

        // Act
        var description = TransformPipelineBuilder.DescribeTransforms(
            mapperRegistry, typeof(MyDescribableCommand));

        // Assert
        Assert.NotNull(description);
        Assert.Empty(description.WrapTransforms);
        Assert.Empty(description.UnwrapTransforms);
    }
}
