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

using Paramore.Brighter.Core.Tests.Validation.TestDoubles;
using Paramore.Brighter.MessageMappers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Validation;

public class MessageMapperRegistryDefaultResolutionTests
{
    [Fact]
    public void When_describing_a_default_mapper_after_use_should_still_identify_as_default()
    {
        // Arrange — an open generic default mapper, with no explicit registration for this request type
        var mapperRegistry = BuildRegistryWithDefaultMapper();

        TransformPipelineBuilder.ClearPipelineCache();

        // Act — resolve the mapper once first, as processing a message does. Get caches the type it
        // resolved so it does not repeat the generic construction per message; describing the pipeline
        // afterwards must still report the mapper as a default one.
        mapperRegistry.Get<MyDescribableCommand>();

        var description = TransformPipelineBuilder.DescribeTransforms(
            mapperRegistry, typeof(MyDescribableCommand));

        // Assert
        Assert.NotNull(description);
        Assert.Equal(typeof(JsonMessageMapper<MyDescribableCommand>), description.MapperType);
        Assert.True(description.IsDefaultMapper);
    }

    [Fact]
    public void When_registering_a_mapper_after_a_default_was_resolved_should_not_report_a_conflict()
    {
        // Arrange
        var mapperRegistry = BuildRegistryWithDefaultMapper();

        // Act — falling back to the default mapper is not a registration, so it must not occupy the
        // slot that an explicit registration for the same request type needs
        mapperRegistry.Get<MyDescribableCommand>();

        // Assert
        mapperRegistry.Register<MyDescribableCommand, MyDescribableCommandMessageMapper>();
        Assert.Equal(
            typeof(MyDescribableCommandMessageMapper),
            mapperRegistry.ResolveMapperInfo(typeof(MyDescribableCommand)).MapperType);
    }

    private static MessageMapperRegistry BuildRegistryWithDefaultMapper() =>
        new(new SimpleMessageMapperFactory(_ => null!),
            new SimpleMessageMapperFactoryAsync(_ => null!),
            defaultMessageMapper: typeof(JsonMessageMapper<>));
}
