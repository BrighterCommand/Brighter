#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class ScopedFactoryDirectCreateResolvesFreshTests
{
    [Fact]
    public void When_a_scoped_mapper_factory_create_is_called_outside_a_pipeline_it_should_resolve_fresh()
    {
        //arrange — an FR-22.2-conformant lifetime triple: all three Scoped; no pipeline scope is offered
        //or supplied, so Create is called directly with the defaulted null scope
        var provider = BuildProvider(services => services.AddScoped<MinimalMapper>());
        using var factory = new ServiceProviderMapperFactory(provider);

        //act — two direct Create calls, neither passing a pipeline scope
        var first = factory.Create(typeof(MinimalMapper));
        var second = factory.Create(typeof(MinimalMapper));

        //assert — two distinct instances; a factory-wide Scoped cache would return the same one twice
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first!.Instance, second!.Instance);
    }

    [Fact]
    public void When_a_scoped_mapper_factory_async_create_is_called_outside_a_pipeline_it_should_resolve_fresh()
    {
        //arrange
        var provider = BuildProvider(services => services.AddScoped<MinimalMapperAsync>());
        using var factory = new ServiceProviderMapperFactoryAsync(provider);

        //act
        var first = factory.Create(typeof(MinimalMapperAsync));
        var second = factory.Create(typeof(MinimalMapperAsync));

        //assert
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first!.Instance, second!.Instance);
    }

    [Fact]
    public void When_a_scoped_transformer_factory_create_is_called_outside_a_pipeline_it_should_resolve_fresh()
    {
        //arrange
        var provider = BuildProvider(services => services.AddScoped<TestTransform>());
        using var factory = new ServiceProviderTransformerFactory(provider);

        //act
        var first = factory.Create(typeof(TestTransform));
        var second = factory.Create(typeof(TestTransform));

        //assert
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first!.Instance, second!.Instance);
    }

    [Fact]
    public void When_a_scoped_transformer_factory_async_create_is_called_outside_a_pipeline_it_should_resolve_fresh()
    {
        //arrange
        var provider = BuildProvider(services => services.AddScoped<TestTransform>());
        using var factory = new ServiceProviderTransformerFactoryAsync(provider);

        //act
        var first = factory.Create(typeof(TestTransform));
        var second = factory.Create(typeof(TestTransform));

        //assert
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first!.Instance, second!.Instance);
    }

    private static IServiceProvider BuildProvider(Action<IServiceCollection> registerArtefact)
    {
        var collection = new ServiceCollection();
        registerArtefact(collection);
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped
        });
        return collection.BuildServiceProvider();
    }
}
