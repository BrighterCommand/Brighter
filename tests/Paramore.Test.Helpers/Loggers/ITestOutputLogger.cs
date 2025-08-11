using Microsoft.Extensions.Logging;
using Paramore.Test.Helpers.TestOutput;

namespace Paramore.Test.Helpers.Loggers
{
    /// <summary>
    /// Represents a logger interface that integrates with test output mechanisms.
    /// This interface extends <see cref="ILogger"/> and provides additional properties
    /// for test output logging, such as <see cref="ITestOutputLoggingProvider"/> and
    /// <see cref="ICoreTestOutputHelper"/>.
    /// </summary>
    public interface ITestOutputLogger : ILogger
    {
        /// <summary>
        /// Gets the logging provider that integrates with test output mechanisms.
        /// This property provides access to an instance of <see cref="ITestOutputLoggingProvider"/>,
        /// which enables logging with external scope support and test output capabilities.
        /// </summary>
        ITestOutputLoggingProvider TestOutputLoggingProvider { get; }

        /// <summary>
        /// Gets the core test output helper associated with this logger.
        /// </summary>
        /// <remarks>
        /// This property provides access to an instance of <see cref="ICoreTestOutputHelper"/>,
        /// which facilitates writing test output and log messages during test execution.
        /// </remarks>
        ICoreTestOutputHelper TestOutputHelper { get; }

        /// <summary>
        /// Gets or sets the minimum <see cref="LogLevel"/> that will be logged by this logger.
        /// </summary>
        /// <remarks>
        /// This property determines the threshold for logging messages. Messages with a 
        /// <see cref="LogLevel"/> lower than the specified value will not be logged.
        /// </remarks>
        LogLevel LogLevel { get; set; }

        /// <summary>
        /// Gets or sets the category name associated with the logger.
        /// This property is used to identify the source or category of log messages,
        /// enabling better organization and filtering of logs.
        /// </summary>
        string LoggerCategoryName { get; set; }
    }

    /// <summary>
    /// Represents a generic logger interface that integrates with test output mechanisms.
    /// This interface extends <see cref="ILogger{TCategoryName}"/> and <see cref="ITestOutputLogger"/>,
    /// providing logging capabilities specific to a category type and test output.
    /// </summary>
    /// <typeparam name="TCategoryName">
    /// The type used to categorize the logger. Typically, this is the type of the class using the logger.
    /// </typeparam>
    public interface ITestOutputLogger<out TCategoryName> : ILogger<TCategoryName>, ITestOutputLogger;
}
