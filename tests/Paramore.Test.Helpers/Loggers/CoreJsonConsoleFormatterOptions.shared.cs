using System.Collections.Generic;
using Microsoft.Extensions.Logging.Console;

namespace Paramore.Test.Helpers.Loggers
{
    /// <summary>
    /// Provides configuration options for the <see cref="JsonConsoleFormatter"/> which formats log messages in JSON format for console output.
    /// </summary>
    public class CoreJsonConsoleFormatterOptions : JsonConsoleFormatterOptions
    {
        /// <summary>
        /// Represents the minimum set of fields included in the JSON console formatter output.
        /// </summary>
        /// <remarks>
        /// The minimum fields include:.
        /// </remarks>
        public static readonly IList<CoreLoggingFormatterOptions> FormatterFieldsMinimum =
        [
            CoreLoggingFormatterOptions.Timestamp,
            CoreLoggingFormatterOptions.Message,
            CoreLoggingFormatterOptions.Exception,
        ];

        /// <summary>
        /// Represents the default set of formatter fields used for normal logging output
        /// in the <see cref="CoreJsonConsoleFormatterOptions"/> class.
        /// </summary>
        /// <remarks>
        /// This field includes the following formatter fields:.
        /// </remarks>
        public static readonly IList<CoreLoggingFormatterOptions> FormatterFieldsNormal =
        [
            CoreLoggingFormatterOptions.Timestamp,
            CoreLoggingFormatterOptions.Category,
            CoreLoggingFormatterOptions.Message,
            CoreLoggingFormatterOptions.Exception,
        ];

        /// <summary>
        /// Represents the default set of formatter fields with scope used for normal logging output
        /// in the <see cref="CoreJsonConsoleFormatterOptions"/> class.
        /// </summary>
        /// <remarks>
        /// This field includes the following formatter fields:.
        /// </remarks>
        public static readonly IList<CoreLoggingFormatterOptions> FormatterFieldsNormalWithScope =
        [
            CoreLoggingFormatterOptions.Timestamp,
            CoreLoggingFormatterOptions.Category,
            CoreLoggingFormatterOptions.Message,
            CoreLoggingFormatterOptions.Exception,
            CoreLoggingFormatterOptions.ScopeProperties,
        ];

        /// <summary>
        /// Represents a predefined list of formatter fields that are included in verbose logging output.
        /// </summary>
        /// <remarks>
        /// This field specifies the set of <see cref="CoreLoggingFormatterOptions"/> that are used
        /// when verbose logging is enabled. It includes fields such as timestamp, event ID, log level,
        /// category, message, exception, scope message, and scope properties.
        /// </remarks>
        public static readonly IList<CoreLoggingFormatterOptions> FormatterFieldsVerbose =
        [
            CoreLoggingFormatterOptions.Timestamp,
            CoreLoggingFormatterOptions.EventId,
            CoreLoggingFormatterOptions.LogLevel,
            CoreLoggingFormatterOptions.Category,
            CoreLoggingFormatterOptions.Message,
            CoreLoggingFormatterOptions.Exception,
            CoreLoggingFormatterOptions.ScopeMessage,
            CoreLoggingFormatterOptions.ScopeProperties,
        ];

        /// <summary>
        /// Initializes a new instance of the <see cref="CoreJsonConsoleFormatterOptions"/> class with the specified formatter fields.
        /// </summary>
        /// <param name="formatterOptions">
        /// The fields to be included in the JSON console formatter.
        /// </param>
        public CoreJsonConsoleFormatterOptions(CoreLoggingFormatterOptions formatterOptions)
        {
            FormatterFields = GetFieldList(formatterOptions);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CoreJsonConsoleFormatterOptions"/> class.
        /// </summary>
        public CoreJsonConsoleFormatterOptions()
         : this(CoreLoggingFormatterOptions.Default)
        {
        }

        /// <summary>
        /// Gets the set of state keys that should be excluded from the log output.
        /// </summary>
        /// <remarks>
        /// This property allows you to specify which state keys should be omitted when formatting log messages.
        /// </remarks>
        public HashSet<string> StateKeysToExclude { get; } = [];

        /// <summary>
        /// Gets or sets the collection of fields to include in the JSON output when formatting log messages.
        /// </summary>
        /// <value>
        /// A list of <see cref="CoreLoggingFormatterOptions"/> that specifies the fields to be included in the formatted output.
        /// </value>
        public IList<CoreLoggingFormatterOptions> FormatterFields { get; set; }

        /// <summary>
        /// Retrieves a list of <see cref="CoreLoggingFormatterOptions"/> based on the specified logging detail level.
        /// </summary>
        /// <param name="formatterOptions">
        /// The <see cref="CoreLoggingFormatterOptions"/> value that specifies the desired logging detail level.
        /// </param>
        /// <returns>
        /// A list of <see cref="CoreLoggingFormatterOptions"/> corresponding to the specified logging detail level.
        /// </returns>
        public static IList<CoreLoggingFormatterOptions> GetFieldList(CoreLoggingFormatterOptions formatterOptions)
        {
            if (formatterOptions.HasFlag(CoreLoggingFormatterOptions.LoggingDetailMinimum))
            {
                return FormatterFieldsMinimum;
            }

            return formatterOptions.HasFlag(CoreLoggingFormatterOptions.LoggingDetailNormalWithScope)
                ? FormatterFieldsNormalWithScope
                : formatterOptions.HasFlag(CoreLoggingFormatterOptions.LoggingDetailVerbose) ? FormatterFieldsVerbose : FormatterFieldsNormal;
        }
    }
}
