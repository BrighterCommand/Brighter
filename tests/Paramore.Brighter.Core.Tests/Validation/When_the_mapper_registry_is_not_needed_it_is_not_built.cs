#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Brighter.Core.Tests.Validation.TestDoubles;
using Paramore.Brighter.MessageMappers;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Validation;

public class ValidationComponentLazyRegistryTests
{
    private static MessageMapperRegistry NewRegistry() => new(
        new SimpleMessageMapperFactory(_ => null!),
        new SimpleMessageMapperFactoryAsync(_ => null!));

    private static Publication PublicationFor<TRequest>(string topic) =>
        new() { Topic = new RoutingKey(topic), RequestType = typeof(TRequest) };

    [Fact]
    public void When_the_validator_has_no_transformer_probe_it_does_not_build_the_registry()
    {
        //arrange — a factory is supplied but no probe, so the wrap-transform check never runs and the
        //registry is never needed. The validator must not build it just because a factory was passed.
        var factoryInvocations = 0;
        Func<MessageMapperRegistry> countingFactory = () =>
        {
            factoryInvocations++;
            return NewRegistry();
        };

        var pipelineBuilder = new PipelineBuilder<IRequest>(new SubscriberRegistry(), loggerFactory: Initializer.TestLoggerFactory);
        PipelineBuilder<IRequest>.ClearPipelineCache();

        var validator = new PipelineValidator(
            pipelineBuilder,
            publications: new[] { PublicationFor<MyDescribableCommand>("greeting") },
            mapperRegistryFactory: countingFactory,
            transformerProbe: null);

        //act
        validator.Validate();

        //assert
        Assert.Equal(0, factoryInvocations);
    }

    [Fact]
    public void When_the_diagnostic_writer_has_no_publications_it_does_not_build_the_registry()
    {
        //arrange — a factory is supplied but there are no publications to describe, so the registry is
        //never needed. The writer must not build it just because a factory was passed.
        var factoryInvocations = 0;
        Func<MessageMapperRegistry> countingFactory = () =>
        {
            factoryInvocations++;
            return NewRegistry();
        };

        var pipelineBuilder = new PipelineBuilder<IRequest>(new SubscriberRegistry(), loggerFactory: Initializer.TestLoggerFactory);
        PipelineBuilder<IRequest>.ClearPipelineCache();

        var writer = new PipelineDiagnosticWriter(
            NullLogger.Instance,
            pipelineBuilder,
            mapperRegistryFactory: countingFactory,
            publications: (IEnumerable<Publication>?)null);

        //act
        writer.Describe();

        //assert
        Assert.Equal(0, factoryInvocations);
    }
}
