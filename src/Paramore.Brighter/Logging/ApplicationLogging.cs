using System;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Paramore.Brighter.Logging
{
    /// <summary>
    /// Provides loggers that follow the application's current logging factory.
    /// </summary>
    public static class ApplicationLogging
    {
        private static ILoggerFactory s_loggerFactory = new LoggerFactory();

        /// <summary>
        /// Gets or sets the factory used by both new and existing Brighter loggers.
        /// </summary>
        /// <value>The process-wide application logging factory.</value>
        /// <remarks>
        /// The caller owns the factory's lifetime. Replacing it does not dispose the previous factory
        /// or transfer scopes that were opened through that factory. The factory is shared process-wide.
        /// </remarks>
        public static ILoggerFactory LoggerFactory
        {
            get => Volatile.Read(ref s_loggerFactory);
            set => Volatile.Write(ref s_loggerFactory, value);
        }

        /// <summary>
        /// Creates a logger for <typeparamref name="T"/> that follows changes to <see cref="LoggerFactory"/>.
        /// </summary>
        /// <typeparam name="T">The type used for the logging category.</typeparam>
        /// <returns>A logger that delegates to the current factory.</returns>
        public static ILogger CreateLogger<T>() => new ForwardingLogger<T>();

        /// <summary>
        /// Creates a logger for <paramref name="type"/> that follows changes to <see cref="LoggerFactory"/>.
        /// </summary>
        /// <param name="type">The type used for the logging category, including static types.</param>
        /// <returns>A logger that delegates to the current factory.</returns>
        public static ILogger CreateLogger(Type type) => new ForwardingLogger(factory => factory.CreateLogger(type));

        private sealed class ForwardingLogger<T>() : ForwardingLogger(factory => factory.CreateLogger<T>()), ILogger<T>;

        private class ForwardingLogger(Func<ILoggerFactory, ILogger> createLogger) : ILogger
        {
            private readonly object _sync = new();
            private LoggerBinding? _binding;

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
                CurrentLogger.BeginScope(state);

            public bool IsEnabled(LogLevel logLevel) => CurrentLogger.IsEnabled(logLevel);

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                CurrentLogger.Log(logLevel, eventId, state, exception, formatter);

            private ILogger CurrentLogger
            {
                get
                {
                    var binding = Volatile.Read(ref _binding);
                    return binding != null && ReferenceEquals(binding.Factory, LoggerFactory)
                        ? binding.Logger
                        : RefreshLogger();
                }
            }

            private ILogger RefreshLogger()
            {
                lock (_sync)
                {
                    var factory = LoggerFactory;
                    var binding = _binding;
                    if (binding == null || !ReferenceEquals(binding.Factory, factory))
                    {
                        binding = new LoggerBinding(factory, createLogger(factory));
                        Volatile.Write(ref _binding, binding);
                    }

                    return binding.Logger;
                }
            }

            private sealed class LoggerBinding(ILoggerFactory factory, ILogger logger)
            {
                public ILoggerFactory Factory { get; } = factory;
                public ILogger Logger { get; } = logger;
            }
        }
    }
}
