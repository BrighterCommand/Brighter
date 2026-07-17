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

using System.Linq;
using Paramore.Brighter.Core.Tests.Validation.TestDoubles;
using Paramore.Brighter.Validation;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.Validation;

public class AsyncOnlyMapperWrapTransformResolvableTests
{
    [Test]
    public async Task When_publication_async_only_mapper_wrap_transform_unresolvable_should_report_warning()
    {
        // Arrange — ONLY an async mapper is registered for the request type; it declares a wrap transform
        // (MyDescribableTransform via the [MyDescribableWrapWith] attribute) the probe cannot resolve. The
        // sync describe path resolves no mapper, so the async-resolved mapper's transforms must still be
        // evaluated for the warning to appear (FR-5 applies to publications as well as subscriptions).
        var registry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => null!),
            new SimpleMessageMapperFactoryAsync(_ => null!));
        registry.RegisterAsync<MyDescribableCommand, MyDescribableCommandMessageMapperAsync>();
        TransformPipelineBuilder.ClearPipelineCache();

        var spec = ProducerValidationRules.WrapTransformResolvable(registry, StubTransformerResolvabilityProbe.ResolvesNothing);
        var publication = new Publication { Topic = new RoutingKey("greeting"), RequestType = typeof(MyDescribableCommand) };

        // Act
        var satisfied = spec.IsSatisfiedBy(publication);
        var results = spec.Accept(new ValidationResultCollector<Publication>()).ToList();

        // Assert — one Warning naming the request type and the GetHandlerType() transformer type
        await Assert.That(satisfied).IsFalse();
        await Assert.That(results).HasSingleItem();
        await Assert.That(results[0].Error!.Severity).IsEqualTo(ValidationSeverity.Warning);
        await Assert.That(results[0].Error!.Message).Contains(nameof(MyDescribableCommand));
        await Assert.That(results[0].Error!.Message).Contains(nameof(MyDescribableTransform));
    }
}