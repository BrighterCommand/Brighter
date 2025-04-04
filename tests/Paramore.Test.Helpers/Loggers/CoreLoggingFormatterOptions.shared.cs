using System;

namespace Paramore.Test.Helpers.Loggers
{
    /// <summary>
    /// Specifies the fields that can be included in the JSON output when formatting log messages
    /// using the <see cref="JsonConsoleFormatter"/>.
    /// </summary>
    /// <remarks>
    /// This enumeration is marked with the <see cref="FlagsAttribute"/>, allowing a combination of
    /// multiple fields to be specified.
    /// </remarks>
    [Flags]
    public enum CoreLoggingFormatterOptions
    {
        /// <summary>
        /// Includes the timestamp of the log entry.
        /// </summary>
        FormatIndented = 1 << 1,

        /// <summary>
        /// Specifies that log messages should be formatted as a single line in the JSON output.
        /// </summary>
        /// <remarks>
        /// This field is useful for scenarios where compact, single-line log entries are preferred,
        /// such as when logs are being processed by systems that expect single-line JSON objects.
        /// </remarks>
        FormatSingleLine = 1 << 2,

        /// <summary>
        /// Includes the scopes in the JSON output when formatting log messages.
        /// </summary>
        /// <remarks>
        /// Scopes provide additional contextual information about the log message,
        /// such as operation names or other metadata. This field can be combined
        /// with other fields using the <see cref="FlagsAttribute"/>.
        /// </remarks>
        FormatIncludeScopes = 1 << 3,

        /// <summary>
        /// Indicates that the JSON output should include timestamps in UTC format when formatting log messages.
        /// </summary>
        /// <remarks>
        /// This field is part of the <see cref="CoreLoggingFormatterOptions"/> enumeration and can be combined
        /// with other fields using bitwise operations due to the <see cref="FlagsAttribute"/> applied to the enumeration.
        /// </remarks>
        FormatUseUtcTimestamp = 1 << 4,

        /// <summary>
        /// Indicates that the JSON output should skip validation when formatting log messages.
        /// </summary>
        /// <remarks>
        /// This field is part of the <see cref="CoreLoggingFormatterOptions"/> enumeration and can be combined
        /// with other fields using bitwise operations due to the <see cref="FlagsAttribute"/> applied to the enumeration.
        /// </remarks>
        FormatSkipValidation = 1 << 5,

        /// <summary>
        /// Represents a mask used to filter or include specific fields in the JSON output
        /// when formatting log messages using the <see cref="JsonConsoleFormatter"/>.
        /// </summary>
        /// <remarks>
        /// The <see cref="FormatMask"/> field is a predefined value that can be used to
        /// isolate or combine fields within the <see cref="CoreLoggingFormatterOptions"/>
        /// enumeration. It is particularly useful for scenarios where a subset of fields
        /// needs to be included in the formatted output.
        /// </remarks>
        FormatMask = 0x000000FF,

        /// <summary>
        /// Represents the default set of fields for the <see cref="CoreLoggingFormatterOptions"/> enumeration.
        /// </summary>
        /// <remarks>
        /// The default configuration includes the following fields:
        /// <list type="bullet">
        /// <item><description><see cref="FormatIndented"/></description></item>
        /// <item><description><see cref="FormatUseUtcTimestamp"/></description></item>
        /// <item><description><see cref="FormatIncludeScopes"/></description></item>
        /// <item><description><see cref="LoggingDetailMinimum"/></description></item>
        /// </list>
        /// </remarks>
        Default = LoggingFormatJson | FormatIndented | FormatUseUtcTimestamp | FormatIncludeScopes | LoggingDetailNormalWithScope,

        /// <summary>
        /// Specifies that the logging output should be formatted as plain text.
        /// </summary>
        LoggingFormatText = 1 << 8,

        /// <summary>
        /// Specifies that the logging output should be formatted as JSON.
        /// </summary>
        /// <remarks>
        /// This flag is used to indicate that the log entries will be serialized in JSON format.
        /// It can be combined with other flags to customize the logging behavior further.
        /// </remarks>
        LoggingFormatJson = 1 << 9,

        /// <summary>
        /// Represents the minimum level of logging detail to be included in the JSON output
        /// when formatting log messages using the <see cref="JsonConsoleFormatter"/>.
        /// </summary>
        /// <remarks>
        /// This field is part of the <see cref="CoreLoggingFormatterOptions"/> enumeration and can be
        /// combined with other fields to customize the logging output.
        /// </remarks>
        LoggingDetailMinimum = 1 << 12,

        /// <summary>
        /// Represents a logging detail level that provides normal verbosity.
        /// This level includes a moderate amount of information suitable for general logging purposes.
        /// </summary>
        LoggingDetailNormal = 1 << 13,

        /// <summary>
        /// Represents a logging detail level that provides normal verbosity with scope.
        /// This level includes a moderate amount of information suitable for general logging purposes.
        /// </summary>
        LoggingDetailNormalWithScope = 1 << 14,

        /// <summary>
        /// Specifies a verbose level of logging detail for the <see cref="CoreLoggingFormatterOptions"/> enumeration.
        /// </summary>
        /// <remarks>
        /// This flag is used to enable the most detailed level of logging output, providing comprehensive information
        /// about the logging events. It is typically used for debugging or in scenarios where maximum logging detail is required.
        /// </remarks>
        LoggingDetailVerbose = 1 << 15,

        /// <summary>
        /// Represents a mask used to filter logging details in the <see cref="CoreLoggingFormatterOptions"/> enumeration.
        /// </summary>
        /// <remarks>
        /// The <see cref="LoggingDetailMask"/> value is a bitmask (0x00FF00) that can be used to isolate or combine
        /// logging detail levels, such as <see cref="LoggingDetailMinimum"/>, <see cref="LoggingDetailNormal"/>,
        /// and <see cref="LoggingDetailVerbose"/>.
        /// </remarks>
        LoggingDetailMask = 0x0000FF00,

        /// <summary>
        /// Includes the timestamp of the log entry.
        /// </summary>
        Timestamp = 1 << 16,

        /// <summary>
        /// Includes the event ID associated with the log entry.
        /// </summary>
        EventId = 1 << 17,

        /// <summary>
        /// Includes the log level (e.g., Information, Warning, Error) of the log entry.
        /// </summary>
        LogLevel = 1 << 18,

        /// <summary>
        /// Includes the category name associated with the log entry.
        /// </summary>
        Category = 1 << 19,

        /// <summary>
        /// Includes the main message of the log entry.
        /// </summary>
        Message = 1 << 20,

        /// <summary>
        /// Includes exception details, if any, associated with the log entry.
        /// </summary>
        Exception = 1 << 21,

        /// <summary>
        /// Includes the scope message, if any, associated with the log entry.
        /// </summary>
        ScopeMessage = 1 << 22,

        /// <summary>
        /// Includes the scope properties, if any, associated with the log entry.
        /// </summary>
        ScopeProperties = 1 << 23,

        /// <summary>
        /// Represents a combination of the <see cref="ScopeMessage"/> and <see cref="ScopeProperties"/> fields.
        /// </summary>
        /// <remarks>
        /// When this field is included in the formatter configuration, both the scope message and its associated
        /// properties will be included in the JSON output. This is useful for scenarios where detailed context
        /// information is required in log entries.
        /// </remarks>
        ScopeMessageWithProperties = ScopeMessage | ScopeProperties,
    }
}
