#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageMappers
{
    public class MessageMapperRegistryDisposalTests
    {
        [Fact]
        public void When_disposing_the_registry_it_disposes_both_mapper_factories()
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
            Assert.NotNull(disposable);

            disposable!.Dispose();

            //assert — both factories were disposed exactly once
            Assert.Equal(1, syncFactory.DisposeCount);
            Assert.Equal(1, asyncFactory.DisposeCount);
        }

        [Fact]
        public void When_disposing_the_registry_twice_it_disposes_each_factory_once()
        {
            //arrange
            var syncFactory = new DisposeCountingMapperFactory();
            var asyncFactory = new DisposeCountingMapperFactoryAsync();
            var registry = new MessageMapperRegistry(syncFactory, asyncFactory);

            //act — a double dispose must be idempotent: a container that disposes the registry after an
            //owner already did must not dispose the factories a second time
            var disposable = registry as IDisposable;
            Assert.NotNull(disposable);

            disposable!.Dispose();
            disposable.Dispose();

            //assert
            Assert.Equal(1, syncFactory.DisposeCount);
            Assert.Equal(1, asyncFactory.DisposeCount);
        }

        [Fact]
        public void When_the_sync_factory_dispose_throws_the_async_factory_is_still_disposed()
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
            Assert.NotNull(disposable);

            var thrown = Assert.Throws<InvalidOperationException>(() => disposable!.Dispose());

            //assert — the sync factory's fault surfaced, and the async factory was disposed regardless
            Assert.Equal("sync factory dispose failed", thrown.Message);
            Assert.Equal(1, asyncFactory.DisposeCount);
        }

        private sealed class ThrowingMapperFactory : IAmAMessageMapperFactory, IDisposable
        {
            public IAmAMessageMapper? Create(Type messageMapperType) => null;

            public void Release(IAmAMessageMapper mapper) { }

            public void Dispose() => throw new InvalidOperationException("sync factory dispose failed");
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
    }
}
