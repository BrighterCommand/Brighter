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

namespace Paramore.Brighter.Extensions.Tests;

public class DefaultOutboxConfigurationTests
{
    [Test]
    public async Task When_custom_box_configuration_set_should_apply_to_default_outbox()
    {
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

        var outbox = provider.GetRequiredService<IAmAnOutbox>();

        await Assert.That(outbox).IsTypeOf<InMemoryOutbox>();
        var inMemoryOutbox = (InMemoryOutbox)outbox;
        await Assert.That(inMemoryOutbox.EntryLimit).IsEqualTo(8192);
        await Assert.That(inMemoryOutbox.EntryTimeToLive).IsEqualTo(TimeSpan.FromMinutes(10));
    }

    [Test]
    public async Task When_no_box_configuration_set_should_use_defaults()
    {
        var services = new ServiceCollection();
        services.AddBrighter()
            .AddProducers(config =>
            {
            });
        var provider = services.BuildServiceProvider();

        var outbox = provider.GetRequiredService<IAmAnOutbox>();

        await Assert.That(outbox).IsTypeOf<InMemoryOutbox>();
        var inMemoryOutbox = (InMemoryOutbox)outbox;
        await Assert.That(inMemoryOutbox.EntryLimit).IsEqualTo(2048);
        await Assert.That(inMemoryOutbox.EntryTimeToLive).IsEqualTo(TimeSpan.FromMinutes(5));
        await Assert.That(inMemoryOutbox.ExpirationScanInterval).IsEqualTo(TimeSpan.FromMinutes(10));
        await Assert.That(inMemoryOutbox.CompactionPercentage).IsEqualTo(0.5);
    }
}
