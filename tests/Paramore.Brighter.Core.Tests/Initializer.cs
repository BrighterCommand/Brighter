using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;

namespace Paramore.Brighter.Core.Tests
{
    static class Initializer
    {
        /// <summary>
        /// A Serilog-backed <see cref="ILoggerFactory"/> wired to Serilog's TestCorrelator sink. Brighter logging
        /// is now instance-scoped, so tests that assert on log output must pass this factory into the Brighter
        /// objects they construct (e.g. as the <c>loggerFactory</c> constructor argument), rather than relying on a
        /// process-wide static.
        /// </summary>
        public static ILoggerFactory TestLoggerFactory { get; private set; } = NullLoggerFactory.Instance;

        [ModuleInitializer]
        public static void InitializeTestLogger()
        {
            var logger = new LoggerConfiguration().WriteTo.TestCorrelator().CreateLogger();
            TestLoggerFactory = new LoggerFactory().AddSerilog(logger);
        }
    }
}
