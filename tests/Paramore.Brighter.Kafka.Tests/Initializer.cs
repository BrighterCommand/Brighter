using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;

namespace Paramore.Brighter.Kafka.Tests
{
    sealed class Initializer
    {
        public static ILoggerFactory TestLoggerFactory { get; private set; } = NullLoggerFactory.Instance;

        [ModuleInitializer]
        public static void InitializeTestLogger()
        {
            var logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.TestCorrelator().CreateLogger();
            TestLoggerFactory = new LoggerFactory().AddSerilog(logger);
        }
    }
}
