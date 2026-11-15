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
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using System.Threading.Tasks;
namespace Paramore.Brighter.Extensions.Tests;

[NotInParallel(LoggerCaptureCollection.NAME)]
public class ConsumerOptionalInboxEventTests
{
    [Test]
    [Arguments(false, false, false)]
    [Arguments(false, false, true)]
    [Arguments(false, true, false)]
    [Arguments(false, true, true)]
    [Arguments(true, false, false)]
    [Arguments(true, false, true)]
    [Arguments(true, true, false)]
    [Arguments(true, true, true)]
    public async Task When_consumers_have_no_inbox_should_handle_duplicate_events(
        bool useOptionsFactory, bool addProducers, bool explicitlyDisableInbox)
    {
        // Arrange
        var bus = new InternalBus();
        var services = new ServiceCollection();

        void Configure(ConsumersOptions options)
        {
            options.DefaultChannelFactory = new InMemoryChannelFactory(bus, TimeProvider.System);
            if (explicitlyDisableInbox)
                options.InboxConfiguration = null;
        }

        IBrighterBuilder builder;
        if (useOptionsFactory)
        {
            builder = services.AddConsumers(_ =>
            {
                var options = new ConsumersOptions();
                Configure(options);
                return options;
            });
        }
        else
        {
            builder = services.AddConsumers(Configure);
        }

        if (addProducers)
        {
            builder.AddProducers(options =>
                options.ProducerRegistry = new InMemoryProducerRegistryFactory(bus,
                    [new Publication { Topic = new RoutingKey("optional-inbox"), RequestType = typeof(ConsumerOptionalInboxEvent) }],
                    InstrumentationOptions.All).Create());
        }

        builder.AutoFromAssemblies();
        using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<IAmACommandProcessor>();
        var request = new ConsumerOptionalInboxEvent();

        // Act
        processor.Publish(request);
        processor.Publish(request);

        // Assert
        await Assert.That(request.HandleCount).IsEqualTo(2);
        await Assert.That(provider.GetRequiredService<IAmConsumerOptions>().InboxConfiguration).IsNull();
        if (useOptionsFactory)
        {
            await Assert.That((await Assert.That(() => provider.GetService<InboxConfiguration>()).ThrowsExactly<ConfigurationException>()).Message).Contains(nameof(InboxConfiguration));
            await Assert.That((await Assert.That(() => provider.GetService<IAmAnInboxSync>()).ThrowsExactly<ConfigurationException>()).Message).Contains(nameof(InboxConfiguration));
            await Assert.That((await Assert.That(() => provider.GetService<IAmAnInboxAsync>()).ThrowsExactly<ConfigurationException>()).Message).Contains(nameof(InboxConfiguration));
        }
        else
        {
            await Assert.That(provider.GetService<InboxConfiguration>()).IsNull();
            await Assert.That(provider.GetService<IAmAnInboxSync>()).IsNull();
            await Assert.That(provider.GetService<IAmAnInboxAsync>()).IsNull();
        }
    }
}
