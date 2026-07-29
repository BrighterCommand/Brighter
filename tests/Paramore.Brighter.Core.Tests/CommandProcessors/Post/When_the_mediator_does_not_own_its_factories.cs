#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Post
{
    /// <summary>
    /// Regression for PR #4254 review finding 4 (producer side). The mediator disposes the mapper registry and
    /// transform factories it was handed. On the DI path it owns a freshly-built graph, so disposal is correct;
    /// on the manual-wiring path the registry is routinely shared with a Dispatcher or another bus, and
    /// unconditional disposal breaks the other owner. Ownership must be declared: unless told it owns them, the
    /// mediator must not dispose a registry or factories it does not own.
    /// </summary>
    public class OutboxProducerMediatorOwnershipTests
    {
        [Fact]
        public void When_the_mediator_does_not_own_its_factories_it_does_not_dispose_them()
        {
            //arrange — the manual-wiring default: the registry/factories are shared with another owner
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

            //act
            mediator.Dispose();

            //assert — nothing the mediator does not own is disposed
            Assert.Equal(0, syncMapperFactory.DisposeCount);
            Assert.Equal(0, asyncMapperFactory.DisposeCount);
            Assert.Equal(0, syncTransformerFactory.DisposeCount);
            Assert.Equal(0, asyncTransformerFactory.DisposeCount);
        }

        [Fact]
        public void When_the_mediator_owns_only_the_registry_it_disposes_only_the_registry()
        {
            //arrange — the two ownership flags are independent
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
                new InMemoryOutbox(timeProvider) { Tracer = tracer },
                ownsRegistry: true,
                ownsTransformerFactories: false);

            //act
            mediator.Dispose();

            //assert — the registry cascade disposes both mapper factories; the transform factories are left alone
            Assert.Equal(1, syncMapperFactory.DisposeCount);
            Assert.Equal(1, asyncMapperFactory.DisposeCount);
            Assert.Equal(0, syncTransformerFactory.DisposeCount);
            Assert.Equal(0, asyncTransformerFactory.DisposeCount);
        }

        [Fact]
        public void When_the_mediator_owns_only_the_transform_factories_it_disposes_only_them()
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
                new InMemoryOutbox(timeProvider) { Tracer = tracer },
                ownsRegistry: false,
                ownsTransformerFactories: true);

            //act
            mediator.Dispose();

            //assert — the transform factories are disposed; the shared registry (and its mapper factories) left alone
            Assert.Equal(0, syncMapperFactory.DisposeCount);
            Assert.Equal(0, asyncMapperFactory.DisposeCount);
            Assert.Equal(1, syncTransformerFactory.DisposeCount);
            Assert.Equal(1, asyncTransformerFactory.DisposeCount);
        }

        private sealed class DisposeCountingMapperFactory : IAmAMessageMapperFactory, IDisposable
        {
            public int DisposeCount { get; private set; }
            public Lease<IAmAMessageMapper>? Create(Type messageMapperType) => null;
            public void Release(Lease<IAmAMessageMapper>? lease) { }
            public void Dispose() => DisposeCount++;
        }

        private sealed class DisposeCountingMapperFactoryAsync : IAmAMessageMapperFactoryAsync, IDisposable
        {
            public int DisposeCount { get; private set; }
            public Lease<IAmAMessageMapperAsync>? Create(Type messageMapperType) => null;
            public void Release(Lease<IAmAMessageMapperAsync>? lease) { }
            public ValueTask ReleaseAsync(Lease<IAmAMessageMapperAsync>? lease) => default;
            public void Dispose() => DisposeCount++;
        }

        private sealed class DisposeCountingTransformerFactory : IAmAMessageTransformerFactory, IDisposable
        {
            public int DisposeCount { get; private set; }
            public Lease<IAmAMessageTransform>? Create(Type transformerType) => null;
            public void Release(Lease<IAmAMessageTransform>? lease) { }
            public void Dispose() => DisposeCount++;
        }

        private sealed class DisposeCountingTransformerFactoryAsync : IAmAMessageTransformerFactoryAsync, IDisposable
        {
            public int DisposeCount { get; private set; }
            public Lease<IAmAMessageTransformAsync>? Create(Type transformerType) => null;
            public void Release(Lease<IAmAMessageTransformAsync>? lease) { }
            public ValueTask ReleaseAsync(Lease<IAmAMessageTransformAsync>? lease) => default;
            public void Dispose() => DisposeCount++;
        }
    }
}
