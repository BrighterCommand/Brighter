using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;

namespace Paramore.Brighter.Extensions.Tests;

public class TransformerFactoryTests
{
    private ServiceProviderTransformerFactory _transformFactory;
    private ServiceProviderTransformerFactoryAsync _transformFactoryAsync;

    [Test]
    public async Task When_resolving_a_transformer_from_the_factory()
    {
       //arrange
       var collection = new ServiceCollection();
       collection.AddSingleton(typeof(TestTransform),new TestTransform());
       collection.AddSingleton<IBrighterOptions>(new BrighterOptions { TransformerLifetime = ServiceLifetime.Singleton });
       var provider = collection.BuildServiceProvider(new ServiceProviderOptions{ValidateOnBuild = true});

       _transformFactory = new ServiceProviderTransformerFactory(provider);
       
       //act
       var testTransform = _transformFactory.Create(typeof(TestTransform));
       
       //assert
       await Assert.That(testTransform).IsNotNull();
    }
    
    [Test]
    public async Task When_resolving_a_transformer_from_the_factory_async()
    {
        //arrange
        var collection = new ServiceCollection();
        collection.AddSingleton(typeof(TestTransform),new TestTransform());
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions { TransformerLifetime = ServiceLifetime.Singleton });
        var provider = collection.BuildServiceProvider(new ServiceProviderOptions{ValidateOnBuild = true});

        _transformFactoryAsync = new ServiceProviderTransformerFactoryAsync(provider);
       
        //act
        var testTransform = _transformFactoryAsync.Create(typeof(TestTransform));
       
        //assert
        await Assert.That(testTransform).IsNotNull();
    }
    
    [Test]
    public async Task When_resolving_a_missing_transformer_from_the_factory()
    {
        //arrange
        var collection = new ServiceCollection();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions { TransformerLifetime = ServiceLifetime.Singleton });
        var provider = collection.BuildServiceProvider();

        _transformFactory = new ServiceProviderTransformerFactory(provider);
       
        //act
        var testTransform = _transformFactory.Create(typeof(TestTransform));
       
        //assert
        await Assert.That(testTransform).IsNull();
    }
    
    [Test]
    public async Task When_resolving_a_missing_transformer_from_the_factory_async()
    {
        //arrange
        var collection = new ServiceCollection();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions { TransformerLifetime = ServiceLifetime.Singleton });
        var provider = collection.BuildServiceProvider();

        _transformFactoryAsync = new ServiceProviderTransformerFactoryAsync(provider);
       
        //act
        var testTransform = _transformFactoryAsync.Create(typeof(TestTransform));
       
        //assert
        await Assert.That(testTransform).IsNull();
    }
}
