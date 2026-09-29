#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;


namespace Paramore.Brighter.Core.Tests.MessageMappers
{
    public class MessageMapperRegistryDisposalTests
    {
        [Test]
        public async System.Threading.Tasks.Task When_disposing_the_registry_it_disposes_both_mapper_factories()
        {
            //arrange
            var syncFactory = new DisposeCountingMapperFactory();
            var asyncFactory = new DisposeCountingMapperFactoryAsync();
            var registry = new MessageMapperRegistry(syncFactory, asyncFactory);

            //act — the registry owns the two factories it was built from. In an IoC host these factories
            //hold the per-resolution IServiceScope of every mapper obtained but not released, and nothing
            //else can reach them to dispose them. Disposing the registry must cascade to both so an owner
            //(the mediator, a validator) can drain them at teardown.
            var disposable = registry as IDisposable;
            await Assert.That(disposable).IsNotNull();

            disposable!.Dispose();

            //assert — both factories were disposed exactly once
            await Assert.That(syncFactory.DisposeCount).IsEqualTo(1);
            await Assert.That(asyncFactory.DisposeCount).IsEqualTo(1);
        }

        [Test]
        public async System.Threading.Tasks.Task When_disposing_the_registry_twice_it_disposes_each_factory_once()
        {
            //arrange
            var syncFactory = new DisposeCountingMapperFactory();
            var asyncFactory = new DisposeCountingMapperFactoryAsync();
            var registry = new MessageMapperRegistry(syncFactory, asyncFactory);

            //act — a double dispose must be idempotent: a container that disposes the registry after an
            //owner already did must not dispose the factories a second time
            var disposable = registry as IDisposable;
            await Assert.That(disposable).IsNotNull();

            disposable!.Dispose();
            disposable.Dispose();

            //assert
            await Assert.That(syncFactory.DisposeCount).IsEqualTo(1);
            await Assert.That(asyncFactory.DisposeCount).IsEqualTo(1);
        }

        [Test]
        public async System.Threading.Tasks.Task When_the_sync_factory_dispose_throws_the_async_factory_is_still_disposed()
        {
            //arrange
            var syncFactory = new ThrowingMapperFactory();
            var asyncFactory = new DisposeCountingMapperFactoryAsync();
            var registry = new MessageMapperRegistry(syncFactory, asyncFactory);

            //act — a user-supplied factory is free to throw from Dispose. The registry disposes both
            //factories it owns; a throw from the first must not skip the second, or the second factory's
            //per-resolution IServiceScope is leaked until the process exits — the retention this Dispose
            //exists to drain. The original exception must still surface to the owner.
            var disposable = registry as IDisposable;
            await Assert.That(disposable).IsNotNull();

            var thrown = await Assert.That(() => disposable!.Dispose()).ThrowsExactly<InvalidOperationException>();

            //assert — the sync factory's fault surfaced, and the async factory was disposed regardless
            await Assert.That(thrown.Message).IsEqualTo("sync factory dispose failed");
            await Assert.That(asyncFactory.DisposeCount).IsEqualTo(1);
        }

        private sealed class ThrowingMapperFactory : IAmAMessageMapperFactory, IDisposable
        {
            public IAmAScope? CreatePipelineScope() => null;

            public Lease<IAmAMessageMapper>? Create(Type messageMapperType, IAmAScope? scope = null) => null;

            public void Release(Lease<IAmAMessageMapper>? lease) { }

            public void Dispose() => throw new InvalidOperationException("sync factory dispose failed");
        }

        private sealed class DisposeCountingMapperFactory : IAmAMessageMapperFactory, IDisposable
        {
            public int DisposeCount { get; private set; }

            public IAmAScope? CreatePipelineScope() => null;

            public Lease<IAmAMessageMapper>? Create(Type messageMapperType, IAmAScope? scope = null) => null;

            public void Release(Lease<IAmAMessageMapper>? lease) { }

            public void Dispose() => DisposeCount++;
        }

        private sealed class DisposeCountingMapperFactoryAsync : IAmAMessageMapperFactoryAsync, IDisposable
        {
            public int DisposeCount { get; private set; }

            public IAmAScope? CreatePipelineScope() => null;

            public Lease<IAmAMessageMapperAsync>? Create(Type messageMapperType, IAmAScope? scope = null) => null;

            public void Release(Lease<IAmAMessageMapperAsync>? lease) { }

            public ValueTask ReleaseAsync(Lease<IAmAMessageMapperAsync>? lease) => default;

            public void Dispose() => DisposeCount++;
        }
    }
}
