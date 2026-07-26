#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Post
{
    public class OutboxProducerMediatorDisposalTests
    {
        [Fact]
        public void When_disposing_the_mediator_it_disposes_the_registry_and_transform_factories()
        {
            //arrange
            var timeProvider = new FakeTimeProvider();

            var syncMapperFactory = new DisposeCountingMapperFactory();
            var asyncMapperFactory = new DisposeCountingMapperFactoryAsync();
            var mapperRegistry = new MessageMapperRegistry(syncMapperFactory, asyncMapperFactory);

            var syncTransformerFactory = new DisposeCountingTransformerFactory();
            var asyncTransformerFactory = new DisposeCountingTransformerFactoryAsync();

            var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>().AddBrighterDefault();
            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>());
            var tracer = new BrighterTracer(timeProvider);

            var mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
                producerRegistry,
                resiliencePipelineRegistry,
                mapperRegistry,
                syncTransformerFactory,
                asyncTransformerFactory,
                tracer,
                new FindPublicationByPublicationTopicOrRequestType(),
                new InMemoryOutbox(timeProvider) { Tracer = tracer });

            //act — the mediator is the sole owner of the runtime mapper/transform graph. The container
            //disposes the mediator (a singleton) at shutdown; the mediator must cascade to the factories
            //that hold per-resolution scopes, otherwise those scopes are retained until the process exits.
            mediator.Dispose();

            //assert — the registry cascade disposes both mapper factories; both transform factories directly
            Assert.Equal(1, syncMapperFactory.DisposeCount);
            Assert.Equal(1, asyncMapperFactory.DisposeCount);
            Assert.Equal(1, syncTransformerFactory.DisposeCount);
            Assert.Equal(1, asyncTransformerFactory.DisposeCount);
        }

        private sealed class DisposeCountingMapperFactory : IAmAMessageMapperFactory, IDisposable
        {
            public int DisposeCount { get; private set; }
            public IAmAMessageMapper? Create(Type messageMapperType) => null;
            public void Release(IAmAMessageMapper mapper) { }
            public void Dispose() => DisposeCount++;
        }

        private sealed class DisposeCountingMapperFactoryAsync : IAmAMessageMapperFactoryAsync, IDisposable
        {
            public int DisposeCount { get; private set; }
            public IAmAMessageMapperAsync? Create(Type messageMapperType) => null;
            public void Release(IAmAMessageMapperAsync mapper) { }
            public ValueTask ReleaseAsync(IAmAMessageMapperAsync mapper) => default;
            public void Dispose() => DisposeCount++;
        }

        private sealed class DisposeCountingTransformerFactory : IAmAMessageTransformerFactory, IDisposable
        {
            public int DisposeCount { get; private set; }
            public IAmAMessageTransform? Create(Type transformerType) => null;
            public void Release(IAmAMessageTransform transformer) { }
            public void Dispose() => DisposeCount++;
        }

        private sealed class DisposeCountingTransformerFactoryAsync : IAmAMessageTransformerFactoryAsync, IDisposable
        {
            public int DisposeCount { get; private set; }
            public IAmAMessageTransformAsync? Create(Type transformerType) => null;
            public void Release(IAmAMessageTransformAsync transformer) { }
            public ValueTask ReleaseAsync(IAmAMessageTransformAsync transformer) => default;
            public void Dispose() => DisposeCount++;
        }
    }
}
