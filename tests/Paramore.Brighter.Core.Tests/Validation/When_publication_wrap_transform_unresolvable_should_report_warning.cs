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
using Paramore.Brighter.MessageMappers;
using Paramore.Brighter.Transforms.Transformers;
using Paramore.Brighter.Validation;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.Validation;

public class WrapTransformResolvableTests
{
    private static MessageMapperRegistry RegistryWith<TMapper>() where TMapper : class, IAmAMessageMapper<MyDescribableCommand>
    {
        var registry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => null!),
            new SimpleMessageMapperFactoryAsync(_ => null!));
        registry.Register<MyDescribableCommand, TMapper>();
        TransformPipelineBuilder.ClearPipelineCache();
        return registry;
    }

    private static Publication PublicationFor<TRequest>(string topic) =>
        new() { Topic = new RoutingKey(topic), RequestType = typeof(TRequest) };

    [Test]
    public async Task When_publication_wrap_transform_unresolvable_should_report_warning()
    {
        // Arrange — mapper declares a wrap transform (MyDescribableTransform) the probe cannot resolve
        var registry = RegistryWith<MyDescribableCommandMessageMapper>();
        var spec = ProducerValidationRules.WrapTransformResolvable(registry, StubTransformerResolvabilityProbe.ResolvesNothing);
        var publication = PublicationFor<MyDescribableCommand>("greeting");

        // Act
        var satisfied = spec.IsSatisfiedBy(publication);
        var results = spec.Accept(new ValidationResultCollector<Publication>()).ToList();

        // Assert — a single Warning naming the request type, transformer type, topic, and AutoFromAssemblies
        await Assert.That(satisfied).IsFalse();
        await Assert.That(results).HasSingleItem();
        await Assert.That(results[0].Error!.Severity).IsEqualTo(ValidationSeverity.Warning);
        await Assert.That(results[0].Error!.Source).Contains("greeting");
        await Assert.That(results[0].Error!.Message).Contains(nameof(MyDescribableCommand));
        await Assert.That(results[0].Error!.Message).Contains(nameof(MyDescribableTransform));
        await Assert.That(results[0].Error!.Message).Contains("greeting");
        await Assert.That(results[0].Error!.Message).Contains("AutoFromAssemblies");
    }

    [Test]
    public async Task When_publication_wrap_transforms_all_resolvable_should_report_no_warning()
    {
        // Arrange — same mapper, but the probe resolves every transformer
        var registry = RegistryWith<MyDescribableCommandMessageMapper>();
        var spec = ProducerValidationRules.WrapTransformResolvable(registry, StubTransformerResolvabilityProbe.ResolvesEverything);
        var publication = PublicationFor<MyDescribableCommand>("greeting");

        // Act
        var satisfied = spec.IsSatisfiedBy(publication);
        var results = spec.Accept(new ValidationResultCollector<Publication>()).ToList();

        // Assert
        await Assert.That(satisfied).IsTrue();
        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task When_publication_mapper_declares_no_transforms_should_report_no_warning()
    {
        // Arrange — vanilla mapper declares no wrap transforms
        var registry = RegistryWith<MyVanillaDescribableCommandMessageMapper>();
        var spec = ProducerValidationRules.WrapTransformResolvable(registry, StubTransformerResolvabilityProbe.ResolvesNothing);
        var publication = PublicationFor<MyDescribableCommand>("greeting");

        // Act
        var satisfied = spec.IsSatisfiedBy(publication);
        var results = spec.Accept(new ValidationResultCollector<Publication>()).ToList();

        // Assert
        await Assert.That(satisfied).IsTrue();
        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task When_publication_request_type_is_null_should_be_skipped()
    {
        // Arrange — a publication with no request type cannot be inspected and must be skipped
        var registry = RegistryWith<MyDescribableCommandMessageMapper>();
        var spec = ProducerValidationRules.WrapTransformResolvable(registry, StubTransformerResolvabilityProbe.ResolvesNothing);
        var publication = new Publication { Topic = new RoutingKey("greeting"), RequestType = null };

        // Act
        var satisfied = spec.IsSatisfiedBy(publication);
        var results = spec.Accept(new ValidationResultCollector<Publication>()).ToList();

        // Assert
        await Assert.That(satisfied).IsTrue();
        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task When_publication_request_type_has_no_mapper_should_report_no_warning()
    {
        // Arrange — no mapper (and no default) resolves for the request type, so there is nothing to inspect
        var registry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => null!),
            new SimpleMessageMapperFactoryAsync(_ => null!));
        TransformPipelineBuilder.ClearPipelineCache();
        var spec = ProducerValidationRules.WrapTransformResolvable(registry, StubTransformerResolvabilityProbe.ResolvesNothing);
        var publication = PublicationFor<MyDescribableCommand>("greeting");

        // Act
        var satisfied = spec.IsSatisfiedBy(publication);
        var results = spec.Accept(new ValidationResultCollector<Publication>()).ToList();

        // Assert
        await Assert.That(satisfied).IsTrue();
        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task When_publication_resolves_to_default_mapper_should_report_no_warning()
    {
        // Arrange — no custom mapper is registered, so the request type resolves to the default
        // JsonMessageMapper, which declares a [CloudEvents] wrap transform. The default mapper's transforms
        // are Brighter built-ins and out of scope, so even a probe that resolves nothing must not warn.
        var registry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => null!),
            new SimpleMessageMapperFactoryAsync(_ => null!),
            typeof(JsonMessageMapper<>),
            typeof(JsonMessageMapper<>));
        TransformPipelineBuilder.ClearPipelineCache();
        var spec = ProducerValidationRules.WrapTransformResolvable(registry, StubTransformerResolvabilityProbe.ResolvesNothing);
        var publication = PublicationFor<MyDescribableCommand>("greeting");

        // Act
        var satisfied = spec.IsSatisfiedBy(publication);
        var results = spec.Accept(new ValidationResultCollector<Publication>()).ToList();

        // Assert — the default mapper's declared transform is skipped, so no warning
        await Assert.That(satisfied).IsTrue();
        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task When_publication_has_resolvable_and_unresolvable_wrap_transforms_should_report_one_warning()
    {
        // Arrange — mapper declares two wrap transforms; the probe resolves one (MyDescribableTransform)
        // but not the other (CompressPayloadTransformer)
        var registry = RegistryWith<MyTwoWrapDescribableCommandMessageMapper>();
        var probe = new StubTransformerResolvabilityProbe(t => t != typeof(CompressPayloadTransformer));
        var spec = ProducerValidationRules.WrapTransformResolvable(registry, probe);
        var publication = PublicationFor<MyDescribableCommand>("greeting");

        // Act
        var satisfied = spec.IsSatisfiedBy(publication);
        var results = spec.Accept(new ValidationResultCollector<Publication>()).ToList();

        // Assert — exactly one Warning, for the unresolvable transform only (resolvable one is not reported)
        await Assert.That(satisfied).IsFalse();
        await Assert.That(results).HasSingleItem();
        await Assert.That(results[0].Error!.Message).Contains(nameof(CompressPayloadTransformer));
        await Assert.That(results[0].Error!.Message).DoesNotContain(nameof(MyDescribableTransform));
    }

    [Test]
    public async Task When_publication_has_only_an_async_custom_mapper_with_a_default_present_should_still_report_warning()
    {
        // Arrange — a default mapper IS configured (as AddBrighter does), but the request type has only a
        // custom async mapper declaring an unresolvable wrap transform. The sync side falls back to the default;
        // the custom async mapper's transform must still be evaluated — the default-mapper guard must not mask
        // a real custom mapper's transforms (FR-5). The probe resolves the default's [CloudEvents] transformer
        // but not the custom one, isolating a single warning.
        var registry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => null!),
            new SimpleMessageMapperFactoryAsync(_ => null!),
            typeof(JsonMessageMapper<>),
            typeof(JsonMessageMapper<>));
        registry.RegisterAsync<MyDescribableCommand, MyDescribableCommandMessageMapperAsync>();
        TransformPipelineBuilder.ClearPipelineCache();
        var probe = new StubTransformerResolvabilityProbe(t => t != typeof(MyDescribableTransform));
        var spec = ProducerValidationRules.WrapTransformResolvable(registry, probe);
        var publication = PublicationFor<MyDescribableCommand>("greeting");

        // Act
        var satisfied = spec.IsSatisfiedBy(publication);
        var results = spec.Accept(new ValidationResultCollector<Publication>()).ToList();

        // Assert — the custom async mapper's wrap transform is still reported
        await Assert.That(satisfied).IsFalse();
        await Assert.That(results).HasSingleItem();
        await Assert.That(results[0].Error!.Message).Contains(nameof(MyDescribableTransform));
    }
}