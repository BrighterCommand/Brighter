using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Test.Helpers.Extensions;
using Paramore.Test.Helpers.TestOutput;

namespace Paramore.Test.Helpers.Loggers
{
    public class TestOutputLogger : ITestOutputLogger
    {
        [ThreadStatic]
        private static StringWriter? _threadStringWriter;

        public TestOutputLogger(ITestOutputLoggingProvider testOutputLoggingProvider, string loggerCategoryName, LogLevel logLevel = LogLevel.Debug)
        {
            TestOutputLoggingProvider = testOutputLoggingProvider ?? throw new ArgumentNullException(nameof(testOutputLoggingProvider));
            LoggerCategoryName = loggerCategoryName;
            LogLevel = logLevel;
        }

        /// <inheritdoc />
        public ITestOutputLoggingProvider TestOutputLoggingProvider { get; }

        /// <inheritdoc />
        public ICoreTestOutputHelper TestOutputHelper => TestOutputLoggingProvider.TestOutputHelper;

        /// <inheritdoc />
        public LogLevel LogLevel { get; set; }

        /// <inheritdoc />
        public string LoggerCategoryName { get; set; }

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (formatter is null)
            {
                throw new ArgumentNullException(nameof(formatter));
            }

            if (IsEnabled(logLevel))
            {
                _threadStringWriter ??= new StringWriter();
                LogEntry<TState> logEntry = new(logLevel, LoggerCategoryName, eventId, state, exception, formatter);

                TestOutputLoggingProvider.Formatter.Write(in logEntry, TestOutputLoggingProvider.ScopeProvider, _threadStringWriter);

                StringBuilder sb = _threadStringWriter.GetStringBuilder();

                if (sb.Length == 0)
                {
                    return;
                }

                string computedAnsiString = sb.ToString();

                sb.Clear();

                if (sb.Capacity > 1024)
                {
                    sb.Capacity = 1024;
                }

                TestOutputHelper.WriteLogLine(computedAnsiString);
            }
        }

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel)
        {
            return LogLevel != LogLevel.None && logLevel >= LogLevel;
        }

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return TestOutputLoggingProvider.ScopeProvider?.Push(state);
        }
    }

    /// <summary>
    /// A generic implementation of <see cref="TestOutputLogger"/> that provides logging capabilities
    /// for a specific category type, integrating with test output mechanisms.
    /// </summary>
    /// <typeparam name="TCategoryName">
    /// The type used to categorize the logger. Typically, this is the type of the class using the logger.
    /// </typeparam>
    public class TestOutputLogger<TCategoryName> : TestOutputLogger, ITestOutputLogger<TCategoryName>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TestOutputLogger{TCategoryName}"/> class.
        /// This constructor sets up a logger for the specified category type, integrating with test output mechanisms.
        /// </summary>
        /// <param name="testOutputLoggingProvider">
        /// The <see cref="ITestOutputLoggingProvider"/> instance used to provide test output logging capabilities.
        /// </param>
        /// <param name="logLevel">
        /// The minimum <see cref="LogLevel"/> for messages to be logged. Defaults to <see cref="LogLevel.Debug"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="testOutputLoggingProvider"/> is <c>null</c>.
        /// </exception>
        public TestOutputLogger(ITestOutputLoggingProvider testOutputLoggingProvider, LogLevel logLevel = LogLevel.Debug)
            : base(testOutputLoggingProvider, typeof(TCategoryName).GetLoggerCategoryName(), logLevel)
        {
        }
    }
}
