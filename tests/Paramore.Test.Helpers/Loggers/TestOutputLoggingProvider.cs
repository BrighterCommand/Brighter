using System;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Paramore.Test.Helpers.TestOutput;

namespace Paramore.Test.Helpers.Loggers
{
    public class TestOutputLoggingProvider : ITestOutputLoggingProvider
    {
        public TestOutputLoggingProvider(ICoreTestOutputHelper testOutputHelper)
        {
            TestOutputHelper = testOutputHelper ?? throw new ArgumentNullException(nameof(testOutputHelper));
            Formatter = new JsonConsoleFormatter(SetDefaultOptions(new CoreJsonConsoleFormatterOptions()));
        }

        public ICoreTestOutputHelper TestOutputHelper { get; }

        public IExternalScopeProvider ScopeProvider { get; set; } = new LoggerExternalScopeProvider();

        public JsonConsoleFormatter Formatter { get; set; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new TestOutputLogger(this, categoryName);
        }

        public void SetScopeProvider(IExternalScopeProvider scopeProvider)
        {
            ScopeProvider = scopeProvider;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // TODO release managed resources here
            }
        }

        /// <summary>
        /// Configures the default options for the <see cref="CoreJsonConsoleFormatterOptions"/>.
        /// </summary>
        /// <param name="options">The options to configure. Cannot be <see langword="null"/>.</param>
        private static CoreJsonConsoleFormatterOptions SetDefaultOptions(CoreJsonConsoleFormatterOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            options.JsonWriterOptions = new JsonWriterOptions() { Indented = true, };
            options.IncludeScopes = true;
            options.UseUtcTimestamp = true;
            options.TimestampFormat = JsonConsoleFormatter.DefaultTimestampFormat;
            options.StateKeysToExclude.Add(JsonConsoleFormatter.KeyOriginalFormat);

            return options;
        }
    }

}
