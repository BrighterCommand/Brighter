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
                Initializer.TestLoggerFactory, new InMemoryOutbox(timeProvider) { Tracer = tracer },
                ownsRegistry: true,
                ownsTransformerFactories: true);

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

        [Fact]
        public void When_closing_the_producers_throws_the_factories_are_still_disposed_and_dispose_is_claimed()
        {
            //arrange
            var timeProvider = new FakeTimeProvider();

            var syncMapperFactory = new DisposeCountingMapperFactory();
            var asyncMapperFactory = new DisposeCountingMapperFactoryAsync();
            var mapperRegistry = new MessageMapperRegistry(syncMapperFactory, asyncMapperFactory);

            var syncTransformerFactory = new DisposeCountingTransformerFactory();
            var asyncTransformerFactory = new DisposeCountingTransformerFactoryAsync();

            var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>().AddBrighterDefault();
            var producerRegistry = new ThrowingProducerRegistry();
            var tracer = new BrighterTracer(timeProvider);

            var mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
                producerRegistry,
                resiliencePipelineRegistry,
                mapperRegistry,
                syncTransformerFactory,
                asyncTransformerFactory,
                tracer,
                new FindPublicationByPublicationTopicOrRequestType(),
                Initializer.TestLoggerFactory, new InMemoryOutbox(timeProvider) { Tracer = tracer },
                ownsRegistry: true,
                ownsTransformerFactories: true);

            //act — CloseAll does broker I/O and can throw at teardown. That failure must not skip the mapper
            //and transform factory disposals, or the per-resolution scopes they retain leak to process exit;
            //and _disposed must be claimed even on the failure path, so a second Dispose() (the container's,
            //after an application-level one) does not re-run CloseAll() on an already-closed registry.
            mediator.Dispose();
            mediator.Dispose();

            //assert — the throw was swallowed, every factory disposed exactly once, and CloseAll ran only once
            Assert.Equal(1, producerRegistry.CloseAllCount);
            Assert.Equal(1, syncMapperFactory.DisposeCount);
            Assert.Equal(1, asyncMapperFactory.DisposeCount);
            Assert.Equal(1, syncTransformerFactory.DisposeCount);
            Assert.Equal(1, asyncTransformerFactory.DisposeCount);
        }

        [Fact]
        public void When_disposing_the_registry_throws_the_transform_factories_are_still_disposed()
        {
            //arrange
            var timeProvider = new FakeTimeProvider();

            //the sync mapper factory throws from Dispose, so the registry's Dispose surfaces an exception
            var syncMapperFactory = new ThrowingMapperFactory();
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
                Initializer.TestLoggerFactory, new InMemoryOutbox(timeProvider) { Tracer = tracer },
                ownsRegistry: true,
                ownsTransformerFactories: true);

            //act — a throw from the registry's Dispose must not skip the two transform factory disposals
            mediator.Dispose();

            //assert — the async mapper factory (disposed in the registry's finally) and both transform
            //factories were disposed despite the sync mapper factory throwing
            Assert.Equal(1, asyncMapperFactory.DisposeCount);
            Assert.Equal(1, syncTransformerFactory.DisposeCount);
            Assert.Equal(1, asyncTransformerFactory.DisposeCount);
        }

        private sealed class ThrowingProducerRegistry : IAmAProducerRegistry
        {
            public int CloseAllCount { get; private set; }
            public void CloseAll()
            {
                CloseAllCount++;
                throw new InvalidOperationException("broker close failed");
            }

            public IAmAMessageProducer LookupBy(RoutingKey topic, CloudEventsType? requestType = null, RequestContext? requestContext = null)
                => throw new NotImplementedException();
            public IAmAMessageProducerAsync LookupAsyncBy(RoutingKey topic, CloudEventsType? requestType = null)
                => throw new NotImplementedException();
            public IAmAMessageProducerSync LookupSyncBy(RoutingKey topic, CloudEventsType? requestType = null)
                => throw new NotImplementedException();
            public IEnumerable<IAmAMessageProducer> Producers => Array.Empty<IAmAMessageProducer>();
            public void Dispose() { }
        }

        private sealed class ThrowingMapperFactory : IAmAMessageMapperFactory, IDisposable
        {
            public Lease<IAmAMessageMapper>? Create(Type messageMapperType) => null;
            public void Release(Lease<IAmAMessageMapper>? lease) { }
            public void Dispose() => throw new InvalidOperationException("sync mapper factory dispose failed");
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
