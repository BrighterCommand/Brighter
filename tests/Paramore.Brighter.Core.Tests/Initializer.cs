using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Logging;
using Serilog;

namespace Paramore.Brighter.Core.Tests
{
    sealed class Initializer
    {
        [ModuleInitializer]
        public static void InitializeTestLogger()
        {
            var logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.TestCorrelator().CreateLogger();
            ApplicationLogging.LoggerFactory = new LoggerFactory().AddSerilog(logger);
            RuntimeHelpers.RunClassConstructor(typeof(RequestHandler<ResilienceContextProbe>).TypeHandle);
            RuntimeHelpers.RunClassConstructor(typeof(RequestHandlerAsync<ResilienceContextProbe>).TypeHandle);
            RuntimeHelpers.RunClassConstructor(typeof(PipelineBuilder<ResilienceContextProbe>).TypeHandle);
        }
    }
}
