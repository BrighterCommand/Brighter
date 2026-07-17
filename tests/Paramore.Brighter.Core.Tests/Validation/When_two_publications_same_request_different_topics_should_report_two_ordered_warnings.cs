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

using System.Collections.Generic;
using System.Linq;
using Paramore.Brighter.Core.Tests.Validation.TestDoubles;
using Paramore.Brighter.Validation;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.Validation;

public class ProducerTransformWarningDeterminismTests
{
    private static MessageMapperRegistry RegistryWithDescribableMapper()
    {
        var registry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => null!),
            new SimpleMessageMapperFactoryAsync(_ => null!));
        registry.Register<MyDescribableCommand, MyDescribableCommandMessageMapper>();
        TransformPipelineBuilder.ClearPipelineCache();
        return registry;
    }

    private static PipelineValidationResult ValidateTwoPublications(MessageMapperRegistry registry)
    {
        var publications = new[]
        {
            new Publication { Topic = new RoutingKey("greeting"), RequestType = typeof(MyDescribableCommand) },
            new Publication { Topic = new RoutingKey("greeting-v2"), RequestType = typeof(MyDescribableCommand) }
        };
        var validator = new PipelineValidator(
            new PipelineBuilder<IRequest>(new SubscriberRegistry()),
            publications,
            transformerProbe: StubTransformerResolvabilityProbe.ResolvesNothing,
            mapperRegistry: registry);
        return validator.Validate();
    }

    [Test]
    public async Task When_two_publications_same_request_different_topics_should_report_two_ordered_warnings()
    {
        // Arrange — two publications for the same request type on different topics, both declaring the same
        // unresolvable wrap transform. Each entity must yield its own warning (no cross-entity de-duplication),
        // in stable publications-order.
        var registry = RegistryWithDescribableMapper();

        // Act
        var result = ValidateTwoPublications(registry);
        var transformWarnings = result.Warnings
            .Where(w => w.Message.Contains(nameof(MyDescribableTransform)))
            .ToList();

        // Assert — exactly two warnings, one per topic, in configuration order
        await Assert.That(transformWarnings.Count).IsEqualTo(2);
        await Assert.That(transformWarnings[0].Source).IsEqualTo("Publication 'greeting'");
        await Assert.That(transformWarnings[1].Source).IsEqualTo("Publication 'greeting-v2'");
    }

    [Test]
    public async Task When_validated_twice_should_report_the_same_warnings_in_the_same_order()
    {
        // Arrange — the same configuration validated twice must produce identical warnings in identical order
        var first = ValidateTwoPublications(RegistryWithDescribableMapper())
            .Warnings.Where(w => w.Message.Contains(nameof(MyDescribableTransform))).Select(w => w.Source).ToList();
        var second = ValidateTwoPublications(RegistryWithDescribableMapper())
            .Warnings.Where(w => w.Message.Contains(nameof(MyDescribableTransform))).Select(w => w.Source).ToList();

        // Assert
        await Assert.That(second).IsEqualTo(first);
    }
}