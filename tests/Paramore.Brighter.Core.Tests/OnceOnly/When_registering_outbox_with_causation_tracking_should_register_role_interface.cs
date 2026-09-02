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
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.OnceOnly
{
    public class CausationTrackingOutboxRegistrationTests
    {
        private static IBrighterBuilder BrighterBuilder(IServiceCollection services)
        {
            var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services);
            services.AddSingleton(subscriberRegistry);
            var mapperRegistry = new ServiceCollectionMessageMapperRegistryBuilder(services);
            services.AddSingleton(mapperRegistry);
            return new ServiceCollectionBrighterBuilder(services, subscriberRegistry, mapperRegistry);
        }

        [Fact]
        public void When_registering_outbox_with_causation_tracking_should_register_role_interface()
        {
            // Arrange — the default outbox (InMemoryOutbox) supports causation tracking
            var services = new ServiceCollection();
            BrighterBuilder(services).AddProducers(config => { });
            var provider = services.BuildServiceProvider();

            // Act
            var trackingOutbox = provider.GetService<IAmACausationTrackingOutbox>();

            // Assert — the outbox is resolvable under its role interface
            Assert.NotNull(trackingOutbox);
        }

        [Fact]
        public void When_registering_outbox_with_causation_tracking_should_resolve_same_instance()
        {
            // Arrange — the default outbox (InMemoryOutbox) supports causation tracking
            var services = new ServiceCollection();
            BrighterBuilder(services).AddProducers(config => { });
            var provider = services.BuildServiceProvider();

            // Act
            var outbox = provider.GetService<IAmAnOutbox>();
            var trackingOutbox = provider.GetService<IAmACausationTrackingOutbox>();

            // Assert — both interfaces resolve to the same singleton instance
            Assert.Same(outbox, trackingOutbox);
        }

        [Fact]
        public void When_registering_outbox_without_causation_tracking_should_not_register_role_interface()
        {
            // Arrange — SpyOutbox does NOT implement IAmACausationTrackingOutbox
            var services = new ServiceCollection();
            BrighterBuilder(services).AddProducers(config =>
            {
                config.Outbox = new SpyOutbox { Tracer = new BrighterTracer(TimeProvider.System) };
                config.TransactionProvider = typeof(SpyTransactionProvider);
            });
            var provider = services.BuildServiceProvider();

            // Act
            var trackingOutbox = provider.GetService<IAmACausationTrackingOutbox>();

            // Assert — no role interface registration for a non-tracking outbox
            Assert.Null(trackingOutbox);
        }
    }
}
