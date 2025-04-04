using Microsoft.Extensions.Logging;
using Paramore.Test.Helpers.TestOutput;

namespace Paramore.Test.Helpers.Loggers
{
    /// <summary>
    /// Provides an interface for a logging provider that integrates with test output mechanisms.
    /// This interface extends <see cref="ILoggerProvider"/> and <see cref="ISupportExternalScope"/>,
    /// enabling the creation of loggers with external scope support and test output capabilities.
    /// </summary>
    public interface ITestOutputLoggingProvider : ILoggerProvider, ISupportExternalScope
    {
        /// <summary>
        /// Gets the core test output helper used for writing test output.
        /// </summary>
        /// <remarks>
        /// This property provides access to an instance of <see cref="ICoreTestOutputHelper"/>,
        /// which facilitates writing test output and managing test-related logging.
        /// </remarks>
        ICoreTestOutputHelper TestOutputHelper { get; }

        /// <summary>
        /// Gets or sets the external scope provider used to manage logging scopes.
        /// </summary>
        /// <remarks>
        /// The <see cref="IExternalScopeProvider"/> allows for managing and propagating
        /// logging scopes across different loggers. This property is essential for
        /// enabling structured logging with contextual information.
        /// </remarks>
        IExternalScopeProvider ScopeProvider { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="JsonConsoleFormatter"/> used for formatting log entries.
        /// </summary>
        /// <remarks>
        /// The formatter is responsible for converting log entries into a structured JSON format,
        /// enabling enhanced readability and integration with structured logging systems.
        /// </remarks>
        JsonConsoleFormatter Formatter { get; set; }
    }
}
