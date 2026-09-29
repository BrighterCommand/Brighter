#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.MessageMappers
{
    public class MessageMapperRegistryWrongTypeReleaseTests
    {
        //A mis-registration - Register(typeof(TargetRequest), typeof(SomeOtherMapper)), or a factory Func
        //that returns the wrong closed type - makes the factory hand Get<TargetRequest>()/GetAsync<..>() a
        //mapper that does not implement IAmAMessageMapper<TargetRequest>. The cast then throws
        //InvalidCastException, but the factory has already created the mapper and, for an IoC-backed
        //factory, opened and tracked the scope it was resolved from. That mapper must be released back to
        //the factory before the exception propagates, or it (and its scope) leaks for the life of the host
        //- the exact create-without-a-paired-release shape this change exists to eliminate.
        [Test]
        public async System.Threading.Tasks.Task When_the_resolved_mapper_is_the_wrong_type_Get_releases_it_before_throwing()
        {
            //arrange — TargetRequest is registered to a mapper that implements IAmAMessageMapper<OtherRequest>,
            //not IAmAMessageMapper<TargetRequest>
            var factory = new WrongTypeTrackingMapperFactory();
            var registry = new MessageMapperRegistry(factory, null);
            registry.Register(typeof(TargetRequest), typeof(WrongMapper));

            //act — the cast in Get<TargetRequest> fails on the wrong closed type
            Exception? exception = null;
            try
            {
                registry.Get<TargetRequest>();
            }
            catch (Exception e)
            {
                exception = e;
            }

            //assert — it throws, and the mapper it created was released back to the factory rather than leaked
            await Assert.That(exception).IsTypeOf<InvalidCastException>();
            await Assert.That(factory.CreateCount).IsEqualTo(1);
            await Assert.That(factory.ReleaseCount).IsEqualTo(factory.CreateCount);
        }

        [Test]
        public async System.Threading.Tasks.Task When_the_resolved_mapper_is_the_wrong_type_GetAsync_releases_it_before_throwing()
        {
            //arrange — TargetRequest is registered to a mapper that implements IAmAMessageMapperAsync<OtherRequest>,
            //not IAmAMessageMapperAsync<TargetRequest>
            var factory = new WrongTypeTrackingMapperFactoryAsync();
            var registry = new MessageMapperRegistry(null, factory);
            registry.RegisterAsync(typeof(TargetRequest), typeof(WrongMapperAsync));

            //act — the cast in GetAsync<TargetRequest> fails on the wrong closed type
            Exception? exception = null;
            try
            {
                registry.GetAsync<TargetRequest>();
            }
            catch (Exception e)
            {
                exception = e;
            }

            //assert — it throws, and the mapper it created was released back to the factory rather than leaked
            await Assert.That(exception).IsTypeOf<InvalidCastException>();
            await Assert.That(factory.CreateCount).IsEqualTo(1);
            await Assert.That(factory.ReleaseCount).IsEqualTo(factory.CreateCount);
        }

        private sealed class TargetRequest : Command
        {
            public TargetRequest() : base(Guid.NewGuid()) { }
        }

        private sealed class OtherRequest : Command
        {
            public OtherRequest() : base(Guid.NewGuid()) { }
        }

        private sealed class WrongMapper : IAmAMessageMapper<OtherRequest>
        {
            public IRequestContext? Context { get; set; }
            public Message MapToMessage(OtherRequest request, Publication publication) => throw new NotImplementedException();
            public OtherRequest MapToRequest(Message message) => throw new NotImplementedException();
        }

        private sealed class WrongMapperAsync : IAmAMessageMapperAsync<OtherRequest>
        {
            public IRequestContext? Context { get; set; }
            public Task<Message> MapToMessageAsync(OtherRequest request, Publication publication, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<OtherRequest> MapToRequestAsync(Message message, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }

        //Counts mappers handed out against mappers handed back and records the released instance, so the
        //test can assert the wrong-typed mapper was returned to the factory rather than orphaned.
        private sealed class WrongTypeTrackingMapperFactory : IAmAMessageMapperFactory
        {
            private int _createCount;
            private int _releaseCount;

            public int CreateCount => _createCount;
            public int ReleaseCount => _releaseCount;

            public IAmAScope? CreatePipelineScope() => null;

            public Lease<IAmAMessageMapper>? Create(Type messageMapperType, IAmAScope? scope = null)
            {
                Interlocked.Increment(ref _createCount);
                return new Lease<IAmAMessageMapper>(new WrongMapper());
            }

            public void Release(Lease<IAmAMessageMapper>? lease) => Interlocked.Increment(ref _releaseCount);
        }

        private sealed class WrongTypeTrackingMapperFactoryAsync : IAmAMessageMapperFactoryAsync
        {
            private int _createCount;
            private int _releaseCount;

            public int CreateCount => _createCount;
            public int ReleaseCount => _releaseCount;

            public IAmAScope? CreatePipelineScope() => null;

            public Lease<IAmAMessageMapperAsync>? Create(Type messageMapperType, IAmAScope? scope = null)
            {
                Interlocked.Increment(ref _createCount);
                return new Lease<IAmAMessageMapperAsync>(new WrongMapperAsync());
            }

            public void Release(Lease<IAmAMessageMapperAsync>? lease) => Interlocked.Increment(ref _releaseCount);

            public ValueTask ReleaseAsync(Lease<IAmAMessageMapperAsync>? lease)
            {
                Interlocked.Increment(ref _releaseCount);
                return default;
            }
        }
    }
}
