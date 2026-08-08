#region Licence
/* The MIT License (MIT)
Copyright © 2026 Miguel Ramirez <xbizzybone@gmail.com>

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

using Paramore.Brighter.Core.Tests.Validation.TestDoubles;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Validation;

public class DescribeTransformsIncludingAsyncTests
{
    private static MessageMapperRegistry EmptyRegistry() => new(
        new SimpleMessageMapperFactory(_ => null!),
        new SimpleMessageMapperFactoryAsync(_ => null!));

    [Fact]
    public void When_describing_multiple_transforms_they_are_unioned_and_ordered_by_descending_step()
    {
        // Arrange — a mapper declaring two distinct wrap transforms: MyDescribableTransform at step 1 and
        // CompressPayloadTransformer at step 0. The union must keep both and order them by descending step.
        var registry = EmptyRegistry();
        registry.Register<MyDescribableCommand, MyTwoWrapDescribableCommandMessageMapper>();
        TransformPipelineBuilder.ClearPipelineCache();

        // Act
        var description = TransformPipelineBuilder.DescribeTransforms(
            registry, typeof(MyDescribableCommand), includeAsync: true);

        // Assert — both transforms present, highest step first
        Assert.NotNull(description);
        Assert.Equal(2, description!.WrapTransforms.Count);
        Assert.Equal(typeof(MyDescribableTransform), description.WrapTransforms[0].TransformType);
        Assert.Equal(1, description.WrapTransforms[0].Step);
        Assert.Equal(typeof(CompressPayloadTransformer), description.WrapTransforms[1].TransformType);
        Assert.Equal(0, description.WrapTransforms[1].Step);
    }

    [Fact]
    public void When_describing_with_async_a_transform_declared_on_both_mappers_is_reported_once()
    {
        // Arrange — the same request type has a sync and an async mapper, each declaring the same wrap
        // transform (MyDescribableTransform at step 0). The union must de-duplicate by (transformer type, step).
        var registry = EmptyRegistry();
        registry.Register<MyDescribableCommand, MyDescribableCommandMessageMapper>();
        registry.RegisterAsync<MyDescribableCommand, MyDescribableCommandMessageMapperAsync>();
        TransformPipelineBuilder.ClearPipelineCache();

        // Act
        var description = TransformPipelineBuilder.DescribeTransforms(
            registry, typeof(MyDescribableCommand), includeAsync: true);

        // Assert — sync ∪ async, de-duplicated to a single wrap transform
        Assert.NotNull(description);
        Assert.Single(description!.WrapTransforms);
        Assert.Equal(typeof(MyDescribableTransform), description.WrapTransforms[0].TransformType);
    }

    [Fact]
    public void When_describing_without_async_an_async_only_mapper_is_not_consulted()
    {
        // Arrange — only an async mapper is registered; with includeAsync false only the sync registry is
        // consulted, which resolves no mapper (and no default), so the description is null.
        var registry = EmptyRegistry();
        registry.RegisterAsync<MyDescribableCommand, MyDescribableCommandMessageMapperAsync>();
        TransformPipelineBuilder.ClearPipelineCache();

        // Act
        var description = TransformPipelineBuilder.DescribeTransforms(
            registry, typeof(MyDescribableCommand), includeAsync: false);

        // Assert
        Assert.Null(description);
    }
}
