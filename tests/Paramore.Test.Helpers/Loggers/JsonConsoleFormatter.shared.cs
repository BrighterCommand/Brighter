using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace Paramore.Test.Helpers.Loggers
{
    /// <summary>
    /// Provides a JSON console formatter for logging, inheriting from <see cref="ConsoleFormatter"/>.
    /// </summary>
    /// <remarks>
    /// This formatter outputs log entries in JSON format, making it suitable for structured logging scenarios.
    /// It supports reloading of options and implements <see cref="IDisposable"/> for resource management.
    /// </remarks>
    public class JsonConsoleFormatter : ConsoleFormatter, IDisposable
    {
        /// <summary>
        /// Represents the key used to format the original message in the logging system.
        /// </summary>
        /// <remarks>
        /// This constant is used within the logging infrastructure to identify and format the original message content.
        /// </remarks>
        public const string KeyOriginalFormat = "{OriginalFormat}";

        /// <summary>
        /// Represents the key used to format the scope in the JSON console formatter.
        /// </summary>
        /// <remarks>
        /// This constant is used to identify and format the scope information in log entries
        /// when using the <see cref="JsonConsoleFormatter"/>.
        /// </remarks>
        public const string KeyFormattedScope = "FormattedScope";

        /// <summary>
        /// The default timestamp format used by the <see cref="JsonConsoleFormatter"/>.
        /// </summary>
        /// <remarks>
        /// This format follows the ISO 8601 standard with high precision, suitable for logging scenarios
        /// where precise timestamps are required. The format is "yyyy-MM-ddTHH:mm:ss.fffffffZ".
        /// </remarks>
        public const string DefaultTimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffffffZ";
        private readonly IDisposable? _optionsReloadToken;
        private bool disposedValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonConsoleFormatter"/> class.
        /// </summary>
        /// <param name="options">The options monitor used to retrieve and manage <see cref="CoreJsonConsoleFormatterOptions"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown when the <paramref name="options"/> parameter is null.</exception>
        /// <remarks>
        /// This constructor sets up the formatter with the provided options and subscribes to changes in the options.
        /// </remarks>
        public JsonConsoleFormatter(IOptionsMonitor<CoreJsonConsoleFormatterOptions> options)
            : base(ConsoleFormatterNames.Json)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            ReloadLoggerOptions(options.CurrentValue);
            _optionsReloadToken = options.OnChange(ReloadLoggerOptions);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonConsoleFormatter"/> class.
        /// </summary>
        /// <param name="options">The options monitor used to retrieve and manage <see cref="CoreJsonConsoleFormatterOptions"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown when the <paramref name="options"/> parameter is null.</exception>
        /// <remarks>
        /// This constructor sets up the formatter with the provided options and subscribes to changes in the options.
        /// </remarks>
        public JsonConsoleFormatter(CoreJsonConsoleFormatterOptions options)
            : base(ConsoleFormatterNames.Json)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            ReloadLoggerOptions(options);
        }

        /// <summary>
        /// Gets or sets the options for configuring the JSON console formatter.
        /// </summary>
        /// <value>
        /// The <see cref="CoreJsonConsoleFormatterOptions"/> used to configure the formatter.
        /// </value>
        public CoreJsonConsoleFormatterOptions FormatterOptions { get; set; }

        /// <summary>
        /// Formats a log message string by replacing placeholders with corresponding values from the provided dictionary.
        /// </summary>
        /// <param name="format">The format string containing placeholders in the form of {key}.</param>
        /// <param name="values">A dictionary containing key-value pairs to replace the placeholders in the format string.</param>
        /// <returns>
        /// A formatted string where placeholders are replaced with corresponding values from the dictionary.
        /// If the format string is null or empty, or if the values dictionary is null, the method returns "null".
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the <paramref name="values"/> parameter is null.</exception>
        public static string FormatLogString(string? format, IDictionary<string, object?> values)
        {
            if (string.IsNullOrEmpty(format) || values is null)
            {
                return "null";
            }

            string pattern = @"\{(\w+)\}";

            return Regex.Replace(format, pattern, match =>
            {
                string key = match.Groups[1].Value;
                return (values.TryGetValue(key, out object? value) ? value : match.Value)?.ToString() ?? "null";
            });
        }

        /// <summary>
        /// Writes a log entry in JSON format to the specified <see cref="TextWriter"/>.
        /// </summary>
        /// <typeparam name="TState">The type of the state object associated with the log entry.</typeparam>
        /// <param name="logEntry">The log entry to write.</param>
        /// <param name="scopeProvider">The provider of scope data.</param>
        /// <param name="textWriter">The writer to which the log entry will be written.</param>
        /// <remarks>
        /// This method formats the log entry as a JSON object and writes it to the provided <see cref="TextWriter"/>.
        /// It includes details such as the log level, category, event ID, timestamp, message, exception (if any),
        /// and state properties.
        /// </remarks>
        public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
        {
            if (textWriter is null)
            {
                throw new ArgumentNullException(nameof(textWriter));
            }

            string message = logEntry.Formatter(logEntry.State, logEntry.Exception);

            if (logEntry.Exception is null && string.IsNullOrEmpty(message))
            {
                return;
            }

            DateTimeOffset stamp = FormatterOptions.TimestampFormat is not null
                ? (FormatterOptions.UseUtcTimestamp ? DateTimeOffset.UtcNow : DateTimeOffset.Now)
                : DateTimeOffset.MinValue;

            // We extract most of the work into a non-generic method to save code size. If this was left in the generic
            // method, we'd get generic specialization for all TState parameters, but that's unnecessary.
            WriteInternal(scopeProvider, textWriter, message, logEntry.LogLevel, logEntry.Category, logEntry.EventId.Id, logEntry.Exception?.ToString(), logEntry.State is not null, logEntry.State?.ToString(), logEntry.State as IReadOnlyList<KeyValuePair<string, object?>>, stamp, FormatterOptions);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the resources used by the <see cref="JsonConsoleFormatter"/> class.
        /// </summary>
        /// <remarks>
        /// This method is called to clean up any resources being used by the formatter, such as
        /// the options reload token. It is part of the implementation of the <see cref="IDisposable"/> interface.
        /// </remarks>
        /// <param name="disposing">
        /// A boolean value indicating whether the method has been called directly or indirectly by a user's code.
        /// If true, both managed and unmanaged resources can be disposed; if false, only unmanaged resources can be disposed.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _optionsReloadToken?.Dispose();
                }

                disposedValue = true;
            }
        }

        private static string GetLogLevelString(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Trace => "Trace",
                LogLevel.Debug => "Debug",
                LogLevel.Information => "Information",
                LogLevel.Warning => "Warning",
                LogLevel.Error => "Error",
                LogLevel.Critical => "Critical",
                _ => throw new ArgumentOutOfRangeException(nameof(logLevel)),
            };
        }

        private static void WriteItem(Utf8JsonWriter writer, KeyValuePair<string, object?> item)
        {
            var key = item.Key;
            switch (item.Value)
            {
                case bool boolValue:
                    writer.WriteBoolean(key, boolValue);
                    break;
                case byte byteValue:
                    writer.WriteNumber(key, byteValue);
                    break;
                case sbyte sbyteValue:
                    writer.WriteNumber(key, sbyteValue);
                    break;
                case char charValue:
#if NETCOREAPP
                    writer.WriteString(key, MemoryMarshal.CreateSpan(ref charValue, 1));
#else
                    writer.WriteString(key, charValue.ToString());
#endif
                    break;
                case decimal decimalValue:
                    writer.WriteNumber(key, decimalValue);
                    break;
                case double doubleValue:
                    writer.WriteNumber(key, doubleValue);
                    break;
                case float floatValue:
                    writer.WriteNumber(key, floatValue);
                    break;
                case int intValue:
                    writer.WriteNumber(key, intValue);
                    break;
                case uint uintValue:
                    writer.WriteNumber(key, uintValue);
                    break;
                case long longValue:
                    writer.WriteNumber(key, longValue);
                    break;
                case ulong ulongValue:
                    writer.WriteNumber(key, ulongValue);
                    break;
                case short shortValue:
                    writer.WriteNumber(key, shortValue);
                    break;
                case ushort ushortValue:
                    writer.WriteNumber(key, ushortValue);
                    break;
                case null:
                    writer.WriteNull(key);
                    break;
                default:
                    writer.WriteString(key, ToInvariantString(item.Value));
                    break;
            }
        }

        private static string? ToInvariantString(object? obj)
        {
            return Convert.ToString(obj, CultureInfo.InvariantCulture);
        }

        private void WriteInternal(
                                    IExternalScopeProvider? scopeProvider,
                                    TextWriter textWriter,
                                    string? message,
                                    LogLevel logLevel,
                                    string category,
                                    int eventId,
                                    string? exception,
                                    bool hasState,
                                    string? stateMessage,
                                    IReadOnlyList<KeyValuePair<string, object?>>? stateProperties,
                                    DateTimeOffset stamp,
                                    CoreJsonConsoleFormatterOptions formatterOptions)
        {
            if (textWriter is null)
            {
                throw new ArgumentNullException(nameof(textWriter));
            }

            const int DefaultBufferSize = 1024;

            using (var output = new CorePooledByteBufferWriter(DefaultBufferSize))
            {
                using (var writer = new Utf8JsonWriter(output, FormatterOptions.JsonWriterOptions))
                {
                    writer.WriteStartObject();

                    foreach (CoreLoggingFormatterOptions formatField in FormatterOptions.FormatterFields)
                    {
                        switch (formatField)
                        {
                            case CoreLoggingFormatterOptions.Timestamp:
                                {
                                    string? timestampFormat = FormatterOptions.TimestampFormat;

                                    if (timestampFormat is not null)
                                    {
                                        writer.WriteString("Timestamp", stamp.ToString(timestampFormat));
                                    }

                                    break;
                                }

                            case CoreLoggingFormatterOptions.EventId:
                                {
                                    writer.WriteNumber(nameof(LogEntry<object>.EventId), eventId);
                                    break;
                                }

                            case CoreLoggingFormatterOptions.LogLevel:
                                {
                                    writer.WriteString(nameof(LogEntry<object>.LogLevel), GetLogLevelString(logLevel));
                                    break;
                                }

                            case CoreLoggingFormatterOptions.Category:
                                {
                                    writer.WriteString(nameof(LogEntry<object>.Category), category);
                                    break;
                                }

                            case CoreLoggingFormatterOptions.Message:
                                {
                                    writer.WriteString("Message", message);
                                    break;
                                }

                            case CoreLoggingFormatterOptions.Exception:
                                {
                                    if (exception is not null)
                                    {
                                        writer.WriteString(nameof(Exception), exception);
                                    }

                                    break;
                                }

                            case CoreLoggingFormatterOptions.ScopeMessageWithProperties:
                            case CoreLoggingFormatterOptions.ScopeProperties:
                            case CoreLoggingFormatterOptions.ScopeMessage:
                                {
                                    bool writtenState = false;

                                    if (hasState)
                                    {
                                        if (formatField.HasFlag(CoreLoggingFormatterOptions.ScopeMessage))
                                        {
                                            writer.WriteStartObject(nameof(LogEntry<object>.State));
                                            writer.WriteString("Message", stateMessage);
                                            writtenState = true;
                                        }

                                        if (stateProperties is not null && formatField.HasFlag(CoreLoggingFormatterOptions.ScopeProperties))
                                        {
                                            foreach (KeyValuePair<string, object?> item in stateProperties)
                                            {
                                                if (formatterOptions.StateKeysToExclude.Contains(item.Key))
                                                {
                                                    continue;
                                                }

                                                if (!writtenState)
                                                {
                                                    writer.WriteStartObject(nameof(LogEntry<object>.State));
                                                    writtenState = true;
                                                }

                                                WriteItem(writer, item);
                                            }
                                        }

                                        if (writtenState)
                                        {
                                            writer.WriteEndObject();
                                        }
                                    }

                                    WriteScopeInformation(writer, scopeProvider, formatterOptions.StateKeysToExclude);

                                    break;
                                }

                            default:
                                {
                                    break;
                                }
                        }
                    }

                    writer.WriteEndObject();
                    writer.Flush();
                }

                ReadOnlySpan<byte> messageBytes = output.WrittenMemory.Span;
                var logMessageBuffer = ArrayPool<char>.Shared.Rent(Encoding.UTF8.GetMaxCharCount(messageBytes.Length));

                try
                {
#if NETSTANDARD2_0_OR_GREATER
                    textWriter.Write(Encoding.UTF8.GetString(output.WrittenMemory.Span.ToArray()));
#else
                    var charsWritten = Encoding.UTF8.GetChars(messageBytes, logMessageBuffer);
                    textWriter.Write(logMessageBuffer, 0, charsWritten);
#endif
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(logMessageBuffer);
                }
            }

            textWriter.Write(",");
        }

        private void WriteScopeInformation(Utf8JsonWriter writer, IExternalScopeProvider? scopeProvider, HashSet<string> stateKeysToExclude)
        {
            bool scopeWritten = false;
            if (FormatterOptions.IncludeScopes && scopeProvider is not null)
            {
                scopeProvider.ForEachScope(
                    (scope, scopeWriter) =>
                    {
                        if (!scopeWritten)
                        {
                            writer.WriteStartArray("Scopes");
                            scopeWritten = true;
                        }

                        var dictionaryKvp = scope as IDictionary<string, object?>;

                        if (dictionaryKvp is null)
                        {
                            if (scope is IDictionary dictionary)
                            {
                                dictionaryKvp = new Dictionary<string, object?>();

                                foreach (object? itemKey in dictionary.Keys)
                                {
                                    string? itemKeyString = itemKey?.ToString();

                                    if (itemKey is not null && !string.IsNullOrEmpty(itemKeyString))
                                    {
                                        dictionaryKvp.Add(new KeyValuePair<string, object?>(itemKeyString!, dictionary[itemKey]));
                                    }
                                }
                            }
                            else if (scope is IEnumerable<KeyValuePair<string, object?>> scopeItems)
                            {
                                dictionaryKvp = scopeItems.ToDictionary();
                            }
                        }

                        if (dictionaryKvp is not null && dictionaryKvp.Any())
                        {
                            scopeWriter.WriteStartObject();

                            foreach (KeyValuePair<string, object?> kvp in dictionaryKvp)
                            {
                                if (kvp.Key.Equals(KeyOriginalFormat, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    WriteItem(scopeWriter, new KeyValuePair<string, object?>(KeyFormattedScope, FormatLogString(kvp.Value?.ToString(), dictionaryKvp)));
                                    continue;
                                }

                                WriteItem(scopeWriter, kvp);
                            }

                            scopeWriter.WriteEndObject();
                        }
                        else
                        {
                            scopeWriter.WriteStringValue(ToInvariantString(scope));
                        }
                    },
                    writer);

                if (scopeWritten)
                {
                    writer.WriteEndArray();
                }
            }
        }

        [MemberNotNull(nameof(FormatterOptions))]
        private void ReloadLoggerOptions(CoreJsonConsoleFormatterOptions options)
        {
            FormatterOptions = options;
        }
    }
}
