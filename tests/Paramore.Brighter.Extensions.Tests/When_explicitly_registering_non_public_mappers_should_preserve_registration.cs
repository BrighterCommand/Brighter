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

using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class ExplicitMapperVisibilityTests
{
    public static IEnumerable<object[]> NonPublicMappers =>
    [
        [typeof(MapperScanCommand), typeof(InternalScanMessageMapper)],
        [typeof(NestedMapperScanCommand), typeof(NestedScanMessageMappers.InternalMapper)],
        [typeof(NestedMapperScanCommand), NestedScanMessageMappers.PrivateMapperType]
    ];

    [Theory]
    [MemberData(nameof(NonPublicMappers))]
    public void When_explicitly_registering_non_public_mappers_should_preserve_registration(
        Type requestType, Type mapperType)
    {
        //Arrange
        var services = new ServiceCollection();
        var registry = new ServiceCollectionMessageMapperRegistryBuilder(services);
        var builder = new ServiceCollectionBrighterBuilder(
            services, new ServiceCollectionSubscriberRegistry(services), registry);

        //Act
        builder.MapperRegistry(mappers =>
        {
            mappers.Add(requestType, mapperType);
            mappers.AddAsync(requestType, mapperType);
        });

        //Assert
        Assert.Equal(mapperType, registry.Mappers[requestType]);
        Assert.Equal(mapperType, registry.AsyncMappers[requestType]);
        using var provider = services.BuildServiceProvider();
        Assert.IsType(mapperType, provider.GetRequiredService(mapperType));
    }
}
