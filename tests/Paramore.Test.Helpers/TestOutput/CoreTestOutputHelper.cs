using System;
using System.Globalization;
using System.Text;
using System.Threading;
using Paramore.Test.Helpers.Base;
using Paramore.Test.Helpers.Extensions;
using Xunit;

namespace Paramore.Test.Helpers.TestOutput
{
    /// <summary>
    /// Class CoreTestOutputHelper. Base class for all xUnit tests.
    /// </summary>
    public class CoreTestOutputHelper : ICoreTestOutputHelper
    {
        private readonly ReaderWriterLockSlim _rwlsTestOutput = new(LockRecursionPolicy.SupportsRecursion);
        private readonly StringBuilder sbTestOutput = new(128);
        private bool isDisposed;
        private bool wroteLog = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="CoreTestOutputHelper"/> class.
        /// </summary>
        /// <param name="testOutputHelper">The test output helper instance.</param>
        public CoreTestOutputHelper(ITestClassBase testCase, ITestOutputHelper testOutputHelper)
        {
            TestCase = testCase;
            WrappedTestOutputHelper = testOutputHelper ?? throw new ArgumentNullException(nameof(testOutputHelper));
        }

        public ITestClassBase TestCase { get; }

        /// <inheritdoc/>
        public ITestOutputHelper WrappedTestOutputHelper { get; }

        /// <inheritdoc/>
        public DateTime DateTimeStart { get; } = DateTime.UtcNow;

        /// <inheritdoc/>
        public string Output => sbTestOutput.ToString();

        /// <inheritdoc/>
        public void Write(string message)
        {
            _rwlsTestOutput.EnterWriteLock();

            try
            {
                sbTestOutput.Append($"{message}");
            }
            finally
            {
                _rwlsTestOutput.ExitWriteLock();
            }
        }

        /// <inheritdoc/>
        public void Write(string format, params object[] args)
        {
            Write($"{string.Format(format, args)}");
        }

        /// <inheritdoc/>
        public void WriteLine(string message)
        {
            _rwlsTestOutput.EnterWriteLock();

            try
            {
                sbTestOutput.AppendLine($"{message}");
            }
            finally
            {
                _rwlsTestOutput.ExitWriteLock();
            }
        }

        /// <inheritdoc/>
        public void WriteLine(string format, params object[] args)
        {
            WriteLine($"{string.Format(format, args)}");
        }

        /// <inheritdoc/>
        public void WriteLine()
        {
            WriteLine(string.Empty);
        }

        /// <inheritdoc/>
        public void WriteLine(bool value)
        {
            WriteLine(value.ToString());
        }

        /// <inheritdoc/>
        public void WriteLine(char value)
        {
            WriteLine(value.ToString());
        }

        /// <inheritdoc/>
        public void WriteLine(decimal value)
        {
            WriteLine(value.ToString(CultureInfo.InvariantCulture));
        }

        /// <inheritdoc/>
        public void WriteLine(double value)
        {
            WriteLine(value.ToString(CultureInfo.InvariantCulture));
        }

        /// <inheritdoc/>
        public void WriteLine(float value)
        {
            WriteLine(value.ToString(CultureInfo.InvariantCulture));
        }

        /// <inheritdoc/>
        public void WriteLine(int value)
        {
            WriteLine(value.ToString());
        }

        /// <inheritdoc/>
        public void WriteLine(uint value)
        {
            WriteLine(value.ToString());
        }

        /// <inheritdoc/>
        public void WriteLine(long value)
        {
            WriteLine(value.ToString());
        }

        /// <inheritdoc/>
        public void WriteLine(ulong value)
        {
            WriteLine(value.ToString());
        }

        /// <inheritdoc/>
        public void WriteLine(object? value)
        {
            WriteLine(value?.ToString() ?? string.Empty);
        }

        /// <inheritdoc/>
        public void WriteLogLine(string message)
        {
            _rwlsTestOutput.EnterWriteLock();
            try
            {
                if (!wroteLog)
                {
                    wroteLog = true;
                    WrappedTestOutputHelper.WriteLine("[");
                }

                WrappedTestOutputHelper.WriteLine(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}\tMessage: {message}");
            }
            finally
            {
                _rwlsTestOutput.ExitWriteLock();
            }
        }

        /// <summary>
        /// Dispose method.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }



        /// <summary>
        /// Disposes and flushes buffer.
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;

            if (disposing)
            {
                _rwlsTestOutput.EnterUpgradeableReadLock();
                DateTime dateTimeNow = DateTime.UtcNow;
                string logEnding = wroteLog ? "]\n" : string.Empty;

                string[] testOutputStrings =
                [
                    $"{TestCase.TestDisplayName}",
                    //$"{this.TestCase.TestClassType.GetTraitOperatingSystem()} {this.TestCase.TestClassType.GetTraitTestType()} {this.TestCase.TestAssembly.GetFrameworkDisplayName()} ({RuntimeInformation.OSArchitecture})",
                    $"S: {DateTimeStart:o}",
                    $"C: {dateTimeNow:o}",
                    $"D: {dateTimeNow - DateTimeStart:c}"
                ];

                try
                {
                    WrappedTestOutputHelper.WriteLine($"{logEnding}{testOutputStrings.CenterTitles('=', ' ', 2)}\n{sbTestOutput}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception:\n{ex.Message}\n{testOutputStrings.CenterTitles('=', ' ', 2)}\n{sbTestOutput}");
                }
                finally
                {
                    _rwlsTestOutput.ExitUpgradeableReadLock();
                }
            }
        }
    }
}
