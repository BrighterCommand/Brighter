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
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class MapperScanStartupTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_starting_with_non_public_duplicate_mappers_should_resolve_public_mappers(bool addConsumers)
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
            Assert.NotNull(provider.GetRequiredService<IAmACommandProcessor>());
            Assert.NotNull(mapper);
            Assert.NotNull(asyncMapper);
            Assert.NotNull(nestedMapper);
            Assert.NotNull(nestedAsyncMapper);
            Assert.IsType<PublicScanMessageMapper>(mapper.Instance);
            Assert.IsType<PublicScanMessageMapper>(asyncMapper.Instance);
            Assert.IsType<NestedScanMessageMappers.PublicMapper>(nestedMapper.Instance);
            Assert.IsType<NestedScanMessageMappers.PublicMapper>(nestedAsyncMapper.Instance);
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
