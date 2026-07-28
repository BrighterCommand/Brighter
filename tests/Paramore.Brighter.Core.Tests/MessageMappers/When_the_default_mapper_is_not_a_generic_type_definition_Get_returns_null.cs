#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageMappers
{
    public class MessageMapperRegistryNonGenericDefaultTests
    {
        //ResolveMapperInfo/ResolveAsyncMapperInfo — the type-only lookup HasPipeline routes through — guard
        //a non-generic default mapper and return (null, true): a default mapper that is not an open generic
        //cannot be closed over the request type, so it is not a usable mapper for that type. Get<T>/GetAsync<T>
        //must agree: they should return null too, not reach MakeGenericType on a non-generic type and throw
        //InvalidOperationException. Otherwise HasPipeline says "no pipeline" while Get says "boom".

        [Fact]
        public void When_the_default_mapper_is_not_a_generic_type_definition_Get_returns_null()
        {
            //arrange — a default mapper that is a closed type, not an open generic (typeof(NonGenericDefaultMapper)
            //.IsGenericTypeDefinition is false), and a request with no explicit registration
            var registry = new MessageMapperRegistry(
                new StubMapperFactory(), null, defaultMessageMapper: typeof(NonGenericDefaultMapper));

            //act
            var mapper = registry.Get<UnregisteredRequest>();

            //assert — null, matching ResolveMapperInfo, rather than throwing InvalidOperationException
            Assert.Null(mapper);
            Assert.Null(registry.ResolveMapperInfo(typeof(UnregisteredRequest)).MapperType);
        }

        [Fact]
        public void When_the_default_mapper_is_not_a_generic_type_definition_GetAsync_returns_null()
        {
            //arrange — a default async mapper that is a closed type, not an open generic, and a request with
            //no explicit registration
            var registry = new MessageMapperRegistry(
                null, new StubMapperFactoryAsync(), defaultMessageMapperAsync: typeof(NonGenericDefaultMapperAsync));

            //act
            var mapper = registry.GetAsync<UnregisteredRequest>();

            //assert — null, matching ResolveAsyncMapperInfo, rather than throwing InvalidOperationException
            Assert.Null(mapper);
            Assert.Null(registry.ResolveAsyncMapperInfo(typeof(UnregisteredRequest)).MapperType);
        }

        private sealed class UnregisteredRequest : Command
        {
            public UnregisteredRequest() : base(Guid.NewGuid()) { }
        }

        private sealed class NonGenericDefaultMapper : IAmAMessageMapper<UnregisteredRequest>
        {
            public IRequestContext? Context { get; set; }
            public Message MapToMessage(UnregisteredRequest request, Publication publication) => throw new NotImplementedException();
            public UnregisteredRequest MapToRequest(Message message) => throw new NotImplementedException();
        }

        private sealed class NonGenericDefaultMapperAsync : IAmAMessageMapperAsync<UnregisteredRequest>
        {
            public IRequestContext? Context { get; set; }
            public Task<Message> MapToMessageAsync(UnregisteredRequest request, Publication publication, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<UnregisteredRequest> MapToRequestAsync(Message message, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }

        private sealed class StubMapperFactory : IAmAMessageMapperFactory
        {
            public Lease<IAmAMessageMapper>? Create(Type messageMapperType) => new Lease<IAmAMessageMapper>(new NonGenericDefaultMapper());
            public void Release(Lease<IAmAMessageMapper> lease) { }
        }

        private sealed class StubMapperFactoryAsync : IAmAMessageMapperFactoryAsync
        {
            public Lease<IAmAMessageMapperAsync>? Create(Type messageMapperType) => new Lease<IAmAMessageMapperAsync>(new NonGenericDefaultMapperAsync());
            public void Release(Lease<IAmAMessageMapperAsync> lease) { }
            public ValueTask ReleaseAsync(Lease<IAmAMessageMapperAsync> lease) => default;
        }
    }
}
