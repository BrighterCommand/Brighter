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

namespace Paramore.Brighter.Core.Tests.Validation;
public class TransformPipelineBuilderDescribeTests
{
    [Test]
    public async Task When_describing_custom_mapper_should_return_mapper_type_and_not_default()
    {
        // Arrange — register an explicit (non-default) mapper for MyDescribableCommand
        var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => null!), new SimpleMessageMapperFactoryAsync(_ => null!));
        mapperRegistry.Register<MyDescribableCommand, MyDescribableCommandMessageMapper>();
        // Act
        var description = TransformPipelineBuilder.DescribeTransforms(mapperRegistry, typeof(MyDescribableCommand));
        // Assert
        await Assert.That(description).IsNotNull();
        await Assert.That(description.MapperType).IsEqualTo(typeof(MyDescribableCommandMessageMapper));
        await Assert.That(description.IsDefaultMapper).IsFalse();
    }

    [Test]
    public async Task When_describing_transforms_should_list_wrap_transforms_in_step_order()
    {
        // Arrange
        var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => null!), new SimpleMessageMapperFactoryAsync(_ => null!));
        mapperRegistry.Register<MyDescribableCommand, MyDescribableCommandMessageMapper>();
        // Act
        var description = TransformPipelineBuilder.DescribeTransforms(mapperRegistry, typeof(MyDescribableCommand));
        // Assert — MyDescribableCommandMessageMapper has [MyDescribableWrapWith(0)] on MapToMessage
        await Assert.That(description).IsNotNull();
        await Assert.That(description.WrapTransforms).HasSingleItem();
        var wrapStep = description.WrapTransforms[0];
        await Assert.That(wrapStep.AttributeType).IsEqualTo(typeof(MyDescribableWrapWith));
        await Assert.That(wrapStep.TransformType).IsEqualTo(typeof(MyDescribableTransform));
        await Assert.That(wrapStep.Step).IsEqualTo(0);
    }

    [Test]
    public async Task When_describing_transforms_should_list_unwrap_transforms_in_step_order()
    {
        // Arrange
        var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => null!), new SimpleMessageMapperFactoryAsync(_ => null!));
        mapperRegistry.Register<MyDescribableCommand, MyDescribableCommandMessageMapper>();
        // Act
        var description = TransformPipelineBuilder.DescribeTransforms(mapperRegistry, typeof(MyDescribableCommand));
        // Assert — MyDescribableCommandMessageMapper has [MyDescribableUnwrapWith(0)] on MapToRequest
        await Assert.That(description).IsNotNull();
        await Assert.That(description.UnwrapTransforms).HasSingleItem();
        var unwrapStep = description.UnwrapTransforms[0];
        await Assert.That(unwrapStep.AttributeType).IsEqualTo(typeof(MyDescribableUnwrapWith));
        await Assert.That(unwrapStep.TransformType).IsEqualTo(typeof(MyDescribableTransform));
        await Assert.That(unwrapStep.Step).IsEqualTo(0);
    }

    [Test]
    public async Task When_describing_default_mapper_should_identify_as_default()
    {
        // Arrange — use an open generic default mapper (no explicit registration for this request type)
        var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => null!), new SimpleMessageMapperFactoryAsync(_ => null!), defaultMessageMapper: typeof(JsonMessageMapper<>));
        // Act
        var description = TransformPipelineBuilder.DescribeTransforms(mapperRegistry, typeof(MyDescribableCommand));
        // Assert
        await Assert.That(description).IsNotNull();
        await Assert.That(description.IsDefaultMapper).IsTrue();
        await Assert.That(description.MapperType).IsEqualTo(typeof(JsonMessageMapper<MyDescribableCommand>));
    }

    [Test]
    public async Task When_describing_vanilla_mapper_should_have_empty_transforms()
    {
        // Arrange — MyVanillaDescribableCommandMessageMapper has no wrap/unwrap attributes
        var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => null!), new SimpleMessageMapperFactoryAsync(_ => null!));
        mapperRegistry.Register<MyDescribableCommand, MyVanillaDescribableCommandMessageMapper>();
        // Act
        var description = TransformPipelineBuilder.DescribeTransforms(mapperRegistry, typeof(MyDescribableCommand));
        // Assert
        await Assert.That(description).IsNotNull();
        await Assert.That(description.WrapTransforms).IsEmpty();
        await Assert.That(description.UnwrapTransforms).IsEmpty();
    }
}
