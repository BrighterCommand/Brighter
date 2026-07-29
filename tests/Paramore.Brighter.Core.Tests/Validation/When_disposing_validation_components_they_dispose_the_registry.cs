#nullable enable
using System;
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Brighter.Core.Tests.Validation.TestDoubles;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Validation
{
    public class ValidationComponentDisposalTests
    {
        private static Publication PublicationFor<TRequest>(string topic) =>
            new() { Topic = new RoutingKey(topic), RequestType = typeof(TRequest) };

        [Fact]
        public void When_disposing_the_validator_it_disposes_the_mapper_registry()
        {
            //arrange — the registry is built lazily, so give the validator a publication and a probe so the
            //wrap-transform check runs and actually builds it; only then does disposal have one to drain.
            var mapperFactory = new DisposeCountingMapperFactory();
            var mapperRegistry = new MessageMapperRegistry(mapperFactory, null);

            var subscriberRegistry = new SubscriberRegistry();
            var pipelineBuilder = new PipelineBuilder<IRequest>(subscriberRegistry);
            PipelineBuilder<IRequest>.ClearPipelineCache();

            var validator = new PipelineValidator(
                pipelineBuilder,
                publications: new[] { PublicationFor<MyDescribableCommand>("greeting") },
                mapperRegistryFactory: () => mapperRegistry,
                transformerProbe: StubTransformerResolvabilityProbe.ResolvesEverything);

            //build the registry through the public API
            validator.Validate();

            //act — the validator is a singleton that owns the validation-time mapper registry it built.
            //The container disposes the validator at shutdown; it must cascade so the registry's factory
            //(and any scope it holds) is drained rather than retained until the process exits.
            var disposable = validator as IDisposable;
            Assert.NotNull(disposable);

            disposable!.Dispose();

            //assert
            Assert.Equal(1, mapperFactory.DisposeCount);
        }

        [Fact]
        public void When_disposing_the_diagnostic_writer_it_disposes_the_mapper_registry()
        {
            //arrange — the registry is built lazily, so give the writer a publication to describe so it
            //actually builds it; only then does disposal have one to drain.
            var mapperFactory = new DisposeCountingMapperFactory();
            var mapperRegistry = new MessageMapperRegistry(mapperFactory, null);

            var subscriberRegistry = new SubscriberRegistry();
            var pipelineBuilder = new PipelineBuilder<IRequest>(subscriberRegistry);
            PipelineBuilder<IRequest>.ClearPipelineCache();

            var writer = new PipelineDiagnosticWriter(
                NullLogger.Instance, pipelineBuilder,
                mapperRegistryFactory: () => mapperRegistry,
                publications: new[] { PublicationFor<MyDescribableCommand>("greeting") });

            //build the registry through the public API
            writer.Describe();

            //act
            var disposable = writer as IDisposable;
            Assert.NotNull(disposable);

            disposable!.Dispose();

            //assert
            Assert.Equal(1, mapperFactory.DisposeCount);
        }

        private sealed class DisposeCountingMapperFactory : IAmAMessageMapperFactory, IDisposable
        {
            public int DisposeCount { get; private set; }
            public Lease<IAmAMessageMapper>? Create(Type messageMapperType) => null;
            public void Release(Lease<IAmAMessageMapper>? lease) { }
            public void Dispose() => DisposeCount++;
        }
    }
}
