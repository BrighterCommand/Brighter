#region Licence

/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Microsoft.Extensions.Options;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class ServiceProviderLambdaTests
{
    [Fact]
    public void AddBrighter_WithServiceProviderFunc_ResolvesServicesCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IAmARequestContextFactory, InMemoryRequestContextFactory>();

        // Act
        services.AddBrighter(sp => new BrighterOptions
        {
            RequestContextFactory = sp.GetRequiredService<IAmARequestContextFactory>()
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IBrighterOptions>();

        // Assert
        Assert.NotNull(options);
        Assert.IsType<InMemoryRequestContextFactory>(options.RequestContextFactory);
    }

    [Fact]
    public void AddBrighter_SupportsPostConfigure_ForTestOverrides()
    {
        // Arrange
        var services = new ServiceCollection();
        var customFactory = new InMemoryRequestContextFactory();

        // Normal registration
        services.AddBrighter(options =>
        {
            options.HandlerLifetime = ServiceLifetime.Scoped;
        });

        // Test override via PostConfigure
        services.PostConfigure<BrighterOptions>(options =>
        {
            options.RequestContextFactory = customFactory;
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IBrighterOptions>();

        // Assert - PostConfigure should have applied
        Assert.Same(customFactory, options.RequestContextFactory);
        Assert.Equal(ServiceLifetime.Scoped, options.HandlerLifetime);
    }

    [Fact]
    public void AddProducers_WithServiceProviderFunc_DefersConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>());
        services.AddSingleton(producerRegistry);

        // Act
        services.AddBrighter()
            .AddProducers(sp => new ProducersConfiguration
            {
                ProducerRegistry = sp.GetRequiredService<ProducerRegistry>()
            });

        var provider = services.BuildServiceProvider();
        var config = provider.GetService<IAmProducersConfiguration>();

        // Assert
        Assert.NotNull(config);
        Assert.Same(producerRegistry, config.ProducerRegistry);
    }

    [Fact]
    public void AddConsumers_WithServiceProviderFunc_ResolvesServicesCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        var internalBus = new InternalBus();
        var channelFactory = new InMemoryChannelFactory(internalBus, TimeProvider.System);
        services.AddSingleton<IAmAChannelFactory>(channelFactory);

        // Act
        services.AddConsumers(sp => new ConsumersOptions
        {
            DefaultChannelFactory = sp.GetRequiredService<IAmAChannelFactory>()
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetService<IAmConsumerOptions>();

        // Assert
        Assert.NotNull(options);
        Assert.Same(channelFactory, options.DefaultChannelFactory);
    }

    [Fact]
    public void AddBrighter_WithActionOverload_StillWorks()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - existing pattern
        services.AddBrighter(options =>
        {
            options.HandlerLifetime = ServiceLifetime.Scoped;
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IBrighterOptions>();

        // Assert
        Assert.Equal(ServiceLifetime.Scoped, options.HandlerLifetime);
    }

    [Fact]
    public void AddBrighter_WithNoConfiguration_UsesDefaults()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddBrighter();

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IBrighterOptions>();

        // Assert
        Assert.Equal(ServiceLifetime.Transient, options.HandlerLifetime);
    }
}
