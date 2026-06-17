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
using Paramore.Brighter.Validation;
using Xunit;

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

    [Fact]
    public void When_publication_wrap_transform_unresolvable_should_report_warning()
    {
        // Arrange — mapper declares a wrap transform (MyDescribableTransform) the probe cannot resolve
        var registry = RegistryWith<MyDescribableCommandMessageMapper>();
        var spec = ProducerValidationRules.WrapTransformResolvable(registry, StubTransformerResolvabilityProbe.ResolvesNothing);
        var publication = PublicationFor<MyDescribableCommand>("greeting");

        // Act
        var satisfied = spec.IsSatisfiedBy(publication);
        var results = spec.Accept(new ValidationResultCollector<Publication>()).ToList();

        // Assert — a single Warning naming the request type, transformer type, topic, and AutoFromAssemblies
        Assert.False(satisfied);
        Assert.Single(results);
        Assert.Equal(ValidationSeverity.Warning, results[0].Error!.Severity);
        Assert.Contains("greeting", results[0].Error!.Source);
        Assert.Contains(nameof(MyDescribableCommand), results[0].Error!.Message);
        Assert.Contains(nameof(MyDescribableTransform), results[0].Error!.Message);
        Assert.Contains("greeting", results[0].Error!.Message);
        Assert.Contains("AutoFromAssemblies", results[0].Error!.Message);
    }

    [Fact]
    public void When_publication_wrap_transforms_all_resolvable_should_report_no_warning()
    {
        // Arrange — same mapper, but the probe resolves every transformer
        var registry = RegistryWith<MyDescribableCommandMessageMapper>();
        var spec = ProducerValidationRules.WrapTransformResolvable(registry, StubTransformerResolvabilityProbe.ResolvesEverything);
        var publication = PublicationFor<MyDescribableCommand>("greeting");

        // Act
        var satisfied = spec.IsSatisfiedBy(publication);
        var results = spec.Accept(new ValidationResultCollector<Publication>()).ToList();

        // Assert
        Assert.True(satisfied);
        Assert.Empty(results);
    }

    [Fact]
    public void When_publication_mapper_declares_no_transforms_should_report_no_warning()
    {
        // Arrange — vanilla mapper declares no wrap transforms
        var registry = RegistryWith<MyVanillaDescribableCommandMessageMapper>();
        var spec = ProducerValidationRules.WrapTransformResolvable(registry, StubTransformerResolvabilityProbe.ResolvesNothing);
        var publication = PublicationFor<MyDescribableCommand>("greeting");

        // Act
        var satisfied = spec.IsSatisfiedBy(publication);
        var results = spec.Accept(new ValidationResultCollector<Publication>()).ToList();

        // Assert
        Assert.True(satisfied);
        Assert.Empty(results);
    }

    [Fact]
    public void When_publication_request_type_is_null_should_be_skipped()
    {
        // Arrange — a publication with no request type cannot be inspected and must be skipped
        var registry = RegistryWith<MyDescribableCommandMessageMapper>();
        var spec = ProducerValidationRules.WrapTransformResolvable(registry, StubTransformerResolvabilityProbe.ResolvesNothing);
        var publication = new Publication { Topic = new RoutingKey("greeting"), RequestType = null };

        // Act
        var satisfied = spec.IsSatisfiedBy(publication);
        var results = spec.Accept(new ValidationResultCollector<Publication>()).ToList();

        // Assert
        Assert.True(satisfied);
        Assert.Empty(results);
    }

    [Fact]
    public void When_publication_request_type_has_no_mapper_should_report_no_warning()
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
        Assert.True(satisfied);
        Assert.Empty(results);
    }
}
