using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class ServiceProviderTransformerFactoryAsyncLeakTests
{
    [Fact]
    public void When_releasing_a_transient_transformer_async_the_factory_should_not_retain_it()
    {
        //arrange
        var collection = new ServiceCollection();
        collection.AddTransient<DisposeCountingTransform>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions { TransformerLifetime = ServiceLifetime.Transient });
        var provider = collection.BuildServiceProvider();

        var factory = new ServiceProviderTransformerFactoryAsync(provider);

        //act
        var transform = (DisposeCountingTransform)factory.Create(typeof(DisposeCountingTransform))!;
        factory.Release(transform);
        var disposeCountAfterRelease = transform.DisposeCount;

        // If the factory's service scope retains the released transient, disposing the factory
        // disposes the instance a second time. A per-message scope that is released on Release
        // leaves nothing for the factory to dispose.
        factory.Dispose();

        //assert
        Assert.Equal(disposeCountAfterRelease, transform.DisposeCount);
    }
}
