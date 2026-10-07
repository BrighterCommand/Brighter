using System;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class MapperFactoryDisposedCreateTests
{
    [Fact]
    public void When_creating_a_mapper_after_the_factory_is_disposed_should_throw()
    {
        //arrange
        var collection = new ServiceCollection();
        collection.AddTransient<NonDisposableMapper>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions { MapperLifetime = ServiceLifetime.Transient });
        var provider = collection.BuildServiceProvider();

        var factory = new ServiceProviderMapperFactory(provider);
        factory.Dispose();

        //act — Dispose drains and clears the tracked scopes, so anything created afterwards could never
        //be released; failing loudly beats handing back an instance whose scope leaks
        var creatingAfterDispose = () => factory.Create(typeof(NonDisposableMapper));

        //assert
        Assert.Throws<ObjectDisposedException>(creatingAfterDispose);
    }

    private sealed class MinimalCommand : Command
    {
        public MinimalCommand() : base(Guid.NewGuid()) { }
    }

    private sealed class NonDisposableMapper : IAmAMessageMapper<MinimalCommand>
    {
        public IRequestContext? Context { get; set; }
        public Message MapToMessage(MinimalCommand request, Publication publication) => throw new NotImplementedException();
        public MinimalCommand MapToRequest(Message message) => throw new NotImplementedException();
    }
}
