using Microsoft.Extensions.Logging;

namespace Paramore.Brighter.Logging
{
    public static class ApplicationLogging
    {
        public static ILoggerFactory LoggerFactory { get; set; } = new LoggerFactory();
        public static ILogger CreateLogger<T>() => LoggerFactory.CreateLogger<T>();
    }
}
