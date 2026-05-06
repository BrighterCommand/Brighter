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
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class DefaultOutboxConfigurationTests
{
    [Fact]
    public void When_custom_box_configuration_set_should_apply_to_default_outbox()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBrighter()
            .AddProducers(config =>
            {
                config.DefaultBoxConfiguration = new InMemoryBoxConfiguration
                {
                    EntryLimit = 8192,
                    EntryTimeToLive = TimeSpan.FromMinutes(10)
                };
            });
        var provider = services.BuildServiceProvider();

        // Act
        var outbox = provider.GetRequiredService<IAmAnOutbox>();

        // Assert — should be an InMemoryOutbox with the custom configuration values
        var inMemoryOutbox = Assert.IsType<InMemoryOutbox>(outbox);
        Assert.Equal(8192, inMemoryOutbox.EntryLimit);
        Assert.Equal(TimeSpan.FromMinutes(10), inMemoryOutbox.EntryTimeToLive);
    }

    [Fact]
    public void When_no_box_configuration_set_should_use_defaults()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBrighter()
            .AddProducers(config =>
            {
                // No DefaultBoxConfiguration set
            });
        var provider = services.BuildServiceProvider();

        // Act
        var outbox = provider.GetRequiredService<IAmAnOutbox>();

        // Assert — should be an InMemoryOutbox with the built-in defaults
        var inMemoryOutbox = Assert.IsType<InMemoryOutbox>(outbox);
        Assert.Equal(2048, inMemoryOutbox.EntryLimit);
        Assert.Equal(TimeSpan.FromMinutes(5), inMemoryOutbox.EntryTimeToLive);
        Assert.Equal(TimeSpan.FromMinutes(10), inMemoryOutbox.ExpirationScanInterval);
        Assert.Equal(0.5, inMemoryOutbox.CompactionPercentage);
    }
}
