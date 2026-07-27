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
        [Fact]
        public void When_disposing_the_validator_it_disposes_the_mapper_registry()
        {
            //arrange
            var mapperFactory = new DisposeCountingMapperFactory();
            var mapperRegistry = new MessageMapperRegistry(mapperFactory, null);

            var subscriberRegistry = new SubscriberRegistry();
            var pipelineBuilder = new PipelineBuilder<IRequest>(subscriberRegistry);
            PipelineBuilder<IRequest>.ClearPipelineCache();

            var validator = new PipelineValidator(pipelineBuilder, mapperRegistryFactory: () => mapperRegistry);

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
            //arrange
            var mapperFactory = new DisposeCountingMapperFactory();
            var mapperRegistry = new MessageMapperRegistry(mapperFactory, null);

            var subscriberRegistry = new SubscriberRegistry();
            var pipelineBuilder = new PipelineBuilder<IRequest>(subscriberRegistry);
            PipelineBuilder<IRequest>.ClearPipelineCache();

            var writer = new PipelineDiagnosticWriter(
                NullLogger.Instance, pipelineBuilder, mapperRegistryFactory: () => mapperRegistry);

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
            public IAmAMessageMapper? Create(Type messageMapperType) => null;
            public void Release(IAmAMessageMapper mapper) { }
            public void Dispose() => DisposeCount++;
        }
    }
}
