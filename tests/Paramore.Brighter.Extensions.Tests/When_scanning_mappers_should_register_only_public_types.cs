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
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class MapperScanVisibilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_scanning_mappers_should_register_only_public_types(bool autoFromAssemblies)
    {
        //Arrange
        var services = new ServiceCollection();
        var registry = new ServiceCollectionMessageMapperRegistryBuilder(services);
        var builder = new ServiceCollectionBrighterBuilder(
            services, new ServiceCollectionSubscriberRegistry(services), registry);
        var assemblies = new[] { typeof(PublicScanMessageMapper).Assembly };

        //Act
        if (autoFromAssemblies)
            builder.AutoFromAssemblies(assemblies);
        else
            builder.MapperRegistryFromAssemblies(assemblies);

        //Assert
        Assert.Equal(typeof(PublicScanMessageMapper), registry.Mappers[typeof(MapperScanCommand)]);
        Assert.Equal(typeof(PublicScanMessageMapper), registry.AsyncMappers[typeof(MapperScanCommand)]);
        Assert.Equal(typeof(NestedScanMessageMappers.PublicMapper), registry.Mappers[typeof(NestedMapperScanCommand)]);
        Assert.Equal(typeof(NestedScanMessageMappers.PublicMapper), registry.AsyncMappers[typeof(NestedMapperScanCommand)]);
        Assert.DoesNotContain(services, service => service.ServiceType == typeof(InternalScanMessageMapper));
        Assert.DoesNotContain(services, service => service.ServiceType == typeof(NestedScanMessageMappers.InternalMapper));
        Assert.DoesNotContain(services, service => service.ServiceType == NestedScanMessageMappers.PrivateMapperType);
        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<PublicScanMessageMapper>());
        Assert.NotNull(provider.GetRequiredService<NestedScanMessageMappers.PublicMapper>());
    }
}
