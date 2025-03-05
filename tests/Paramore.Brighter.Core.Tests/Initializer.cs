using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Serilog;

namespace Paramore.Brighter.Core.Tests
{
    sealed class Initializer
    {
        [ModuleInitializer]
        public static void InitializeTestLogger()
        {
            var logger = new LoggerConfiguration().WriteTo.TestCorrelator().CreateLogger();
            ApplicationLogging.LoggerFactory = new LoggerFactory().AddSerilog(logger);
        }
    }
}
