#nullable enable
using System;
using System.Collections.Generic;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.Testing;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch
{
    /// <summary>
    /// The drain-on-dispose wait is bounded by <see cref="Dispatcher.ShutdownTimeout"/>, which is configurable
    /// so a consumer with long-running handlers (for example video processing) can allow its in-flight message
    /// more time to finish before teardown. It defaults to 10 seconds and can be set through the constructor
    /// (which the DI setup option and the dispatch builder thread through).
    /// </summary>
    public class DispatcherShutdownTimeoutConfigurationTests
    {
        [Fact]
        public void When_no_shutdown_timeout_is_supplied_it_defaults_to_ten_seconds()
        {
            var dispatcher = BuildDispatcher(shutdownTimeout: null);

            Assert.Equal(TimeSpan.FromSeconds(10), dispatcher.ShutdownTimeout);
        }

        [Fact]
        public void When_a_shutdown_timeout_is_supplied_it_is_honoured()
        {
            var configured = TimeSpan.FromMinutes(5);

            var dispatcher = BuildDispatcher(shutdownTimeout: configured);

            Assert.Equal(configured, dispatcher.ShutdownTimeout);
        }

        private static Dispatcher BuildDispatcher(TimeSpan? shutdownTimeout)
        {
            IAmACommandProcessor commandProcessor = new SpyCommandProcessor();
            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
                null);
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

            return new Dispatcher(
                commandProcessor,
                new List<Subscription>(),
                messageMapperRegistry,
                shutdownTimeout: shutdownTimeout);
        }
    }
}
