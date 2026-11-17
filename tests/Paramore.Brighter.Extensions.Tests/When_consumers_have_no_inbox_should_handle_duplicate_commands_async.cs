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
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

[Collection(LoggerCaptureCollection.NAME)]
public class ConsumerOptionalInboxAsyncCommandTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task When_consumers_have_no_inbox_should_handle_duplicate_commands_async(
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
                    [new Publication { Topic = new RoutingKey("optional-inbox"), RequestType = typeof(ConsumerGlobalInboxAsyncCommand) }],
                    InstrumentationOptions.All).Create());
        }

        builder.AutoFromAssemblies();
        using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<IAmACommandProcessor>();
        var request = new ConsumerGlobalInboxAsyncCommand();

        // Act
        await processor.SendAsync(request);
        await processor.SendAsync(request);

        // Assert
        Assert.Equal(2, request.HandleCount);
        Assert.Null(provider.GetRequiredService<IAmConsumerOptions>().InboxConfiguration);
        if (useOptionsFactory)
        {
            Assert.Contains(nameof(InboxConfiguration), Assert.Throws<ConfigurationException>(
                () => provider.GetService<InboxConfiguration>()).Message);
            Assert.Contains(nameof(InboxConfiguration), Assert.Throws<ConfigurationException>(
                () => provider.GetService<IAmAnInboxSync>()).Message);
            Assert.Contains(nameof(InboxConfiguration), Assert.Throws<ConfigurationException>(
                () => provider.GetService<IAmAnInboxAsync>()).Message);
        }
        else
        {
            Assert.Null(provider.GetService<InboxConfiguration>());
            Assert.Null(provider.GetService<IAmAnInboxSync>());
            Assert.Null(provider.GetService<IAmAnInboxAsync>());
        }
    }
}
