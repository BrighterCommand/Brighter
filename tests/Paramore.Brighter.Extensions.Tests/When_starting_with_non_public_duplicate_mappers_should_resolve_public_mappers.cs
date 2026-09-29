#region Licence

/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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

using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;


namespace Paramore.Brighter.Extensions.Tests;

public class MapperScanStartupTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async System.Threading.Tasks.Task When_starting_with_non_public_duplicate_mappers_should_resolve_public_mappers(bool addConsumers)
    {
        //Arrange
        var services = new ServiceCollection();

        //Act
        var builder = addConsumers ? services.AddConsumers() : services.AddBrighter();
        builder.AutoFromAssemblies();
        using var provider = services.BuildServiceProvider();
        using var registry = Paramore.Brighter.Extensions.DependencyInjection.ServiceCollectionExtensions
            .MessageMapperRegistry(provider);
        var mapper = registry.Get<MapperScanCommand>();
        var asyncMapper = registry.GetAsync<MapperScanCommand>();
        var nestedMapper = registry.Get<NestedMapperScanCommand>();
        var nestedAsyncMapper = registry.GetAsync<NestedMapperScanCommand>();

        try
        {
            //Assert
            await Assert.That(provider.GetRequiredService<IAmACommandProcessor>()).IsNotNull();
            await Assert.That(mapper).IsNotNull();
            await Assert.That(asyncMapper).IsNotNull();
            await Assert.That(nestedMapper).IsNotNull();
            await Assert.That(nestedAsyncMapper).IsNotNull();
            await Assert.That(mapper.Instance).IsTypeOf<PublicScanMessageMapper>();
            await Assert.That(asyncMapper.Instance).IsTypeOf<PublicScanMessageMapper>();
            await Assert.That(nestedMapper.Instance).IsTypeOf<NestedScanMessageMappers.PublicMapper>();
            await Assert.That(nestedAsyncMapper.Instance).IsTypeOf<NestedScanMessageMappers.PublicMapper>();
        }
        finally
        {
            registry.Release(mapper);
            registry.Release(asyncMapper);
            registry.Release(nestedMapper);
            registry.Release(nestedAsyncMapper);
        }
    }
}
