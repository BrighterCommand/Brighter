using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Paramore.Brighter.Logging
{
    /// <summary>
    /// Provides a process-wide <see cref="ILoggerFactory"/> for creating loggers.
    /// </summary>
    /// <remarks>
    /// This static accessor is obsolete. Brighter logging is now instance-scoped: the runtime objects Brighter
    /// constructs receive an <see cref="ILoggerFactory"/> (flowed from the DI container, from
    /// <c>CommandProcessorBuilder.ConfigureLogging</c>/<c>DispatchBuilder.ConfigureLogging</c>, or via the optional
    /// <c>loggerFactory</c> constructor parameters added across the codebase). The DI extensions no longer copy the
    /// container's <see cref="ILoggerFactory"/> into this static, which previously caused use-after-dispose when the
    /// container was disposed and cross-talk between two Brighter instances in the same process.
    /// It remains only as a crash-free, no-op fallback for code that has not yet been migrated, and will be removed
    /// in a future release.
    /// </remarks>
    [Obsolete("Brighter logging is now instance-scoped; inject an ILoggerFactory (via the DI extensions, " +
              "CommandProcessorBuilder.ConfigureLogging/DispatchBuilder.ConfigureLogging, or the optional loggerFactory " +
              "constructor parameters). This static will be removed in a future release.")]
    public static class ApplicationLogging
    {
        /// <summary>
        /// The logger factory used by this static accessor. Defaults to a no-op factory; nothing in Brighter writes
        /// to it any longer. Setting it does not affect Brighter's instance-scoped logging.
        /// </summary>
        public static ILoggerFactory LoggerFactory { get; set; } = NullLoggerFactory.Instance;

        /// <summary>
        /// Creates a logger from <see cref="LoggerFactory"/>. Tolerates a disposed factory by returning a no-op logger
        /// rather than throwing.
        /// </summary>
        public static ILogger CreateLogger<T>()
        {
            try
            {
                return LoggerFactory.CreateLogger<T>();
            }
            catch (ObjectDisposedException)
            {
                return NullLogger<T>.Instance;
            }
        }
    }
}
