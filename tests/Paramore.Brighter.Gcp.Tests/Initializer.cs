using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Serilog;

namespace Paramore.Brighter.Gcp.Tests
{
    sealed class Initializer
    {
        public static ILoggerFactory Factory { get; private set; } = new LoggerFactory();
        [ModuleInitializer]
        public static void InitializeTestLogger()
        {
            var logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.TestCorrelator().CreateLogger();
            Factory = new LoggerFactory().AddSerilog(logger);
        }
    }
}
